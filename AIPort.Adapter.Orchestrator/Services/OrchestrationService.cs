using System.Data.Common;
using System.Net.Sockets;
using System.Text.Json;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using AIPort.Adapter.Orchestrator.Agi;
using AIPort.Adapter.Orchestrator.Config;
using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Adapter.Orchestrator.Services;

public sealed class OrchestrationService : IOrchestrationService
{
    private static readonly Regex ApartmentKeywordPattern = new(
        @"\b(?:apto?\.?|apartamento|unidade|sala)\s*[n°#]?\s*([A-Za-z0-9]{1,10})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BlockPattern = new(
        @"\bbloco\s*[n°#]?\s*([A-Za-z0-9]{1,6})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TowerPattern = new(
        @"\btorre\s*[n°#]?\s*([A-Za-z0-9]{1,10})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FillerWordsPattern = new(
        @"\b(?:apartamento|apto|unidade|sala|numero|número|n°|num)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BareSlotTokenPattern = new(
        @"^[A-Za-z0-9]{1,10}$",
        RegexOptions.Compiled);

    private readonly ITenantRepository _tenantRepository;
    private readonly ICallSessionRepository _callSessionRepository;
    private readonly ISpeechToTextService _stt;
    private readonly ITextToSpeechService _tts;
    private readonly IIntelligenceServiceClient _intelligence;
    private readonly IDecisionExecutor _executor;
    private readonly IAsteriskCommandClient _agi;
    private readonly IEventService _eventService;
    private readonly FallbackRoutingOptions _fallbackRoutingOptions;
    private readonly ILogger<OrchestrationService> _logger;

    public OrchestrationService(
        ITenantRepository tenantRepository,
        ICallSessionRepository callSessionRepository,
        ISpeechToTextService stt,
        ITextToSpeechService tts,
        IIntelligenceServiceClient intelligence,
        IDecisionExecutor executor,
        IAsteriskCommandClient agi,
        IEventService eventService,
        IOptions<FallbackRoutingOptions> fallbackRoutingOptions,
        ILogger<OrchestrationService> logger)
    {
        _tenantRepository = tenantRepository;
        _callSessionRepository = callSessionRepository;
        _stt = stt;
        _tts = tts;
        _intelligence = intelligence;
        _executor = executor;
        _agi = agi;
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        _fallbackRoutingOptions = fallbackRoutingOptions.Value;
        _logger = logger;
    }

    public async Task<CallOrchestrationResult> HandleCallAsync(AgiCallContext call, CancellationToken ct = default)
    {
        var startedAtUtc = call.StartedAtUtc;
        Tenant? tenant = null;
        TenantResponsesDto? responses = null;
        InferenceResponseDto? iaResponse = null;
        CallSession? callSession = null;
        VisitorInputCollectionResult? visitorInput = null;
        string? texto = null;
        string acaoExecutada = "NAO_EXECUTADO";
        string respostaFalada = string.Empty;

        _logger.LogInformation(
            "Iniciando orquestração Session={SessionId} TenantPid={TenantPid} CallerId={CallerId} Channel={Channel}",
            call.SessionId,
            call.TenantPid,
            call.CallerId,
            call.Channel);

        // Publica evento de chamada iniciada
        _eventService.PublishCallStarted("Tenant Pid:" + call.TenantPid, call.CallerId ?? "Desconhecido");

        try
        {
            try
            {
                tenant = await _tenantRepository.GetByPidAsync(call.TenantPid, ct);
            }
            catch (Exception ex) when (IsDatabaseUnavailable(ex))
            {
                return await RedirectToCentralWhenDatabaseOfflineAsync(call, ex, ct);
            }

            if (tenant is null)
            {
                _logger.LogWarning(
                    "Tenant não encontrado/inativo para Pid={TenantPid}. Session={SessionId}",
                    call.TenantPid,
                    call.SessionId);

                if (call.VoiceChannel is not null)
                {
                    try
                    {
                        var fallbackMessage = "Não foi possível identificar este local. Encerrando atendimento.";
                        var fallbackFile = await _tts.SynthesizeAsync(fallbackMessage, ct);
                        await call.VoiceChannel.PlayAsync(fallbackFile, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao reproduzir fallback de tenant não encontrado.");
                    }
                }

                _eventService.PublishError($"Tenant não encontrado para PID {call.TenantPid}", "TENANT_ERROR");

                return new CallOrchestrationResult
                {
                    SessionId = call.SessionId,
                    AcaoExecutada = "TENANT_NAO_ENCONTRADO",
                    RespostaFalada = "Não foi possível identificar este local.",
                    Sucesso = false,
                    MotivoFalha = "Tenant inativo ou inexistente para o PID informado."
                };
            }

            var tenantType = MapTenantType(tenant.TipoLocal);

            callSession = new CallSession
            {
                SessionId = call.SessionId,
                TenantId = tenant.Id,
                CallerId = call.CallerId,
                Channel = call.Channel,
                StartedAt = startedAtUtc.UtcDateTime,
                EndedAt = null,
                FinalAction = null,
                FinalExtractedData = "{}"
            };

            await _callSessionRepository.CreateSessionAsync(callSession, ct);

            // Busca os templates de voz do tenant uma única vez para reutilizar ao longo da chamada.
            try
            {
                responses = await _intelligence.GetTenantResponsesAsync(tenantType, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao buscar respostas do tenant '{TenantType}'. Usando padrão.", tenantType);
                responses = new TenantResponsesDto();
            }

            visitorInput = await CollectVisitorInputAsync(call, tenant, responses, ct);
            texto = visitorInput.Texto ?? string.Empty;

            if (!call.EscalateDueToSilence && call.VoiceChannel is not null)
            {
                // Toca mensagem de espera antes de enviar para a IA, mantendo o canal AGI ativo
                // e informando o visitante de que o processamento está em andamento.
                try
                {
                    var aguardarFile = await _tts.SynthesizeAsync(responses.Aguardar, ct);
                    await call.VoiceChannel.PlayAsync(aguardarFile, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao reproduzir mensagem de espera. Prosseguindo.");
                }
            }

            var iaRequest = new InferenceRequestDto
            {
                Texto = texto,
                TenantType = tenantType,
                SessionId = call.SessionId,
                Metadata = BuildInferenceMetadata(call, tenant)
            };

            // Quando o slot-filling residencial já coletou todos os dados obrigatórios
            // (VisitorName, ResidentName, Apartment, Block, Document), constrói a resposta
            // diretamente a partir do VisitContext e pula a chamada ao serviço de inferência,
            // indo direto para a etapa de notificação do morador.
            if (call.VisitContext?.IsComplete == true)
            {
                var ctx = call.VisitContext;
                _logger.LogInformation(
                    "Slot-filling completo Session={SessionId} — bypassando inferência e notificando morador.",
                    call.SessionId);

                iaResponse = new InferenceResponseDto
                {
                    Intencao          = "VISITA_RESIDENCIAL",
                    AcaoSugerida      = "NOTIFICAR_MORADOR",
                    RespostaTexto     = "Aguarde, estamos notificando o morador.",
                    CamadaResolucao   = "SLOT_FILLING",
                    Confianca         = 1.0,
                    DadosExtraidos    = new DadosExtraidosDto
                    {
                        NomeVisitante      = ctx.VisitorName,
                        Nome               = ctx.ResidentName,
                        Unidade            = ctx.Apartment,
                        Bloco              = ctx.Block,
                        Torre              = ctx.Tower,
                        Documento          = ctx.Document,
                        TemDadosExtraidos  = true
                    }
                };
            }
            else if (call.EscalateDueToSilence)
            {
                _logger.LogInformation(
                    "Silêncio consecutivo detectado Session={SessionId}; escalando para atendimento humano.",
                    call.SessionId);

                iaResponse = new InferenceResponseDto
                {
                    Intencao = "INDEFINIDA",
                    AcaoSugerida = "ESCALAR_HUMANO",
                    RespostaTexto = "Não estou conseguindo ouvir sua resposta. Vou transferir para atendimento humano.",
                    CamadaResolucao = "NO_INPUT_GUARD",
                    Confianca = 1.0,
                    DadosExtraidos = call.VisitContext is null
                        ? new DadosExtraidosDto { TemDadosExtraidos = false }
                        : new DadosExtraidosDto
                        {
                            NomeVisitante = call.VisitContext.VisitorName,
                            Nome = call.VisitContext.ResidentName,
                            Unidade = call.VisitContext.Apartment,
                            Bloco = call.VisitContext.Block,
                            Torre = call.VisitContext.Tower,
                            Documento = call.VisitContext.Document,
                            TemDadosExtraidos = true
                        }
                };
            }
            else
            {
                iaResponse = await _intelligence.ProcessAsync(iaRequest, ct);
            }

            await RegisterCollectedInputInteractionIfNeededAsync(call, visitorInput, iaResponse, ct);

            (string AcaoExecutada, string RespostaFalada) execution;
            try
            {
                execution = await _executor.ExecuteAsync(call, tenant, iaResponse, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Falha de infraestrutura externa durante execução da decisão Session={SessionId}. Aplicando fallback humano.",
                    call.SessionId);
                execution = (
                    "ESCALAR_HUMANO",
                    "Não foi possível completar a notificação automática. Encaminhando para atendimento humano.");
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Timeout de infraestrutura externa durante execução da decisão Session={SessionId}. Aplicando fallback humano.",
                    call.SessionId);
                execution = (
                    "ESCALAR_HUMANO",
                    "Não foi possível completar a notificação automática. Encaminhando para atendimento humano.");
            }

            acaoExecutada = execution.AcaoExecutada;
            respostaFalada = execution.RespostaFalada;

            // Publica evento de conclusão bem-sucedida
            _eventService.PublishEvent(
                level: "info",
                message: $"Chamada finalizada com sucesso para {tenant.NomeIdentificador}: {execution.AcaoExecutada}",
                category: "CALL_SUCCESS",
                data: new Dictionary<string, object>
                {
                    { "sessionId", call.SessionId },
                    { "tenantName", tenant.NomeIdentificador },
                    { "action", execution.AcaoExecutada }
                }
            );

            return new CallOrchestrationResult
            {
                SessionId = call.SessionId,
                AcaoExecutada = execution.AcaoExecutada,
                RespostaFalada = respostaFalada,
                Sucesso = true,
                Intencao = iaResponse.Intencao,
                CamadaResolucao = iaResponse.CamadaResolucao,
                Confianca = iaResponse.Confianca,
                DadosExtraidos = iaResponse.DadosExtraidos,
                Debug = iaResponse.Debug
            };
        }
        catch (AgiHangupException)
        {
            acaoExecutada = iaResponse?.AcaoSugerida ?? "CHAMADA_ENCERRADA";
            _logger.LogInformation("Canal AGI encerrado pelo Asterisk durante atendimento Session={SessionId}.", call.SessionId);

            _eventService.PublishCallEnded(tenant?.NomeIdentificador ?? "Desconhecido", "Hangup pelo Asterisk");

            return new CallOrchestrationResult
            {
                SessionId = call.SessionId,
                AcaoExecutada = acaoExecutada,
                RespostaFalada = "",
                Sucesso = false,
                MotivoFalha = "Canal encerrado pelo Asterisk.",
                Intencao = iaResponse?.Intencao,
                CamadaResolucao = iaResponse?.CamadaResolucao,
                Confianca = iaResponse?.Confianca,
                DadosExtraidos = iaResponse?.DadosExtraidos,
                Debug = iaResponse?.Debug
            };
        }
        catch (Exception ex)
        {
            acaoExecutada = iaResponse?.AcaoSugerida ?? "ERRO_ORQUESTRACAO";
            _logger.LogError(ex, "Falha durante atendimento Session={SessionId}", call.SessionId);

            _eventService.PublishError(
                $"Falha na orquestração para {tenant?.NomeIdentificador ?? "Desconhecido"}: {ex.Message}",
                "ORCHESTRATION_ERROR"
            );

            throw;
        }
        finally
        {
            if (callSession is not null)
            {
                try
                {
                    var finalExtractedData = BuildFinalAuditJson(call, tenant, iaResponse, acaoExecutada, respostaFalada);

                    await RegisterFinalInteractionIfNeededAsync(call, iaResponse, acaoExecutada, finalExtractedData, ct);

                    await _callSessionRepository.CompleteSessionAsync(
                        call.SessionId,
                        acaoExecutada,
                        finalExtractedData,
                        DateTime.UtcNow,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao finalizar CallSession Session={SessionId}", call.SessionId);
                }
            }
        }
    }

    private async Task<CallOrchestrationResult> RedirectToCentralWhenDatabaseOfflineAsync(
        AgiCallContext call,
        Exception ex,
        CancellationToken ct)
    {
        const string message = "Estamos com instabilidade no sistema. Seu atendimento sera redirecionado para a central.";
        var targetExtension = _fallbackRoutingOptions.DatabaseOfflineExtension?.Trim();
        var transferred = false;

        _logger.LogWarning(ex,
            "Base de dados indisponivel ao buscar tenant por PID={TenantPid}. Session={SessionId}. Aplicando fallback para central.",
            call.TenantPid,
            call.SessionId);

        if (call.VoiceChannel is not null)
        {
            try
            {
                var fallbackFile = await _tts.SynthesizeAsync(message, ct);
                await call.VoiceChannel.PlayAsync(fallbackFile, ct);
            }
            catch (Exception playbackEx)
            {
                _logger.LogWarning(playbackEx,
                    "Falha ao reproduzir mensagem de fallback por banco offline. Session={SessionId}",
                    call.SessionId);
            }
        }

        if (!string.IsNullOrWhiteSpace(targetExtension))
        {
            try
            {
                await _agi.TransferAsync(call, targetExtension, ct);
                transferred = true;
            }
            catch (Exception transferEx)
            {
                _logger.LogError(transferEx,
                    "Falha ao transferir chamada para a central de fallback '{TargetExtension}'. Session={SessionId}",
                    targetExtension,
                    call.SessionId);
            }
        }
        else
        {
            _logger.LogError(
                "FallbackRouting:DatabaseOfflineExtension nao esta configurado. Session={SessionId}",
                call.SessionId);
        }

        _eventService.PublishWarning(
            $"Banco indisponivel durante roteamento da chamada {call.SessionId}. Fallback central acionado.",
            "DATABASE_OFFLINE_FALLBACK");

        return new CallOrchestrationResult
        {
            SessionId = call.SessionId,
            AcaoExecutada = transferred ? "ESCALAR_HUMANO" : "ERRO_BANCO_DADOS",
            RespostaFalada = message,
            Sucesso = transferred,
            MotivoFalha = transferred ? null : "Base de dados indisponivel e a transferencia para a central nao foi concluida."
        };
    }

    private static bool IsDatabaseUnavailable(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is DbException or TimeoutException or SocketException)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reproduz a saudação e a solicitação de identificação do tenant, grava a fala do
    /// visitante e retorna a transcrição para processamento pela IA.
    /// </summary>
    private async Task<VisitorInputCollectionResult> CollectVisitorInputAsync(
        AgiCallContext call, Tenant tenant, TenantResponsesDto responses, CancellationToken ct)
    {
        if (call.VoiceChannel is null)
        {
            var transcribedText = await _stt.TranscribeAsync(call.AudioFilePath, call.PreTranscribedText, ct);
            return new VisitorInputCollectionResult(transcribedText, null, 0, true);
        }

        if (string.Equals(MapTenantType(tenant.TipoLocal), "residential", StringComparison.OrdinalIgnoreCase))
            return await CollectResidentialVisitorInputAsync(call, tenant, responses, ct);

        // Toca a saudação específica do tenant (ex.: "Olá! Bem-vindo ao Condomínio...")
        var greetingMessage = BuildGreetingMessage(tenant, responses);
        var greetingFile = await _tts.SynthesizeAsync(greetingMessage, ct);
        await call.VoiceChannel.PlayAsync(greetingFile, ct);

        // Solicita identificação / informa o que o visitante deve dizer
        var askFile = await _tts.SynthesizeAsync(responses.SolicitarDocumento, ct);
        await call.VoiceChannel.PlayAsync(askFile, ct);

        // Grava a resposta do visitante (máx 7 s ou silêncio)
        var interactionTimer = Stopwatch.StartNew();
        var safeUniqueId = call.UniqueId.Replace('.', '-');
        var basePath = Path.Combine("/tmp", $"agi-{safeUniqueId}-{Guid.NewGuid():N}");
        var record = await call.VoiceChannel.RecordAsync(basePath, maxTimeMs: 7000, ct);

        var transcript = await _stt.TranscribeAsync(basePath + ".wav", call.PreTranscribedText, ct);
        if (string.IsNullOrWhiteSpace(transcript) && record.DigitPressed is not null)
            transcript = $"DTMF:{record.DigitPressed}";
        interactionTimer.Stop();

        call.AudioFilePath = basePath + ".wav";
        return new VisitorInputCollectionResult(
            transcript,
            string.Join(Environment.NewLine, new[] { greetingMessage, responses.SolicitarDocumento }.Where(x => !string.IsNullOrWhiteSpace(x))),
            interactionTimer.ElapsedMilliseconds,
            false);
    }

    private async Task<VisitorInputCollectionResult> CollectResidentialVisitorInputAsync(
        AgiCallContext call,
        Tenant tenant,
        TenantResponsesDto responses,
        CancellationToken ct)
    {
        var context = new VisitContext
        {
            RequiresBlock = tenant.UsaBloco,
            RequiresTower = tenant.UsaTorre
        };
        var tenantType = MapTenantType(tenant.TipoLocal);
        var conversation = new List<string>();
        var consecutiveSilentRounds = 0;
        const int maxConsecutiveSilentRounds = 1;

        var greetingMessage = BuildGreetingMessage(tenant, responses);
        await PlayPromptAsync(call, greetingMessage, ct);

        const int maxRounds = 8;
        for (int round = 0; round < maxRounds; round++)
        {
            if (context.IsComplete)
                break;

            var nextPrompt = context.GetNextPrompt();
            if (nextPrompt is null)
                break;

            var expectedSlot = GetExpectedSlot(context);
            var promptToUse = context.GetConfirmationPrompt() ?? nextPrompt;
            var capture = await AskAndCaptureAsync(call, promptToUse, ct);
            var answer = capture.Transcript;
            var llmProcessingTimeMs = 0L;
            string? resolutionLayer = null;

            conversation.Add(FormatConversationTurn(round + 1, answer));

            if (string.IsNullOrWhiteSpace(answer))
            {
                consecutiveSilentRounds++;
                if (consecutiveSilentRounds >= maxConsecutiveSilentRounds)
                {
                    _logger.LogInformation(
                        "Encerrando slot-filling por silêncio consecutivo Session={SessionId} Round={Round}.",
                        call.SessionId,
                        round);
                    break;
                }
            }
            else
            {
                consecutiveSilentRounds = 0;
            }

            if (!string.IsNullOrWhiteSpace(answer))
            {
                var llmTimer = Stopwatch.StartNew();
                try
                {
                    var extractRequest = new InferenceRequestDto
                    {
                        Texto = answer,
                        TenantType = tenantType,
                        SessionId = call.SessionId,
                        Metadata = BuildInferenceMetadata(call, tenant, slotFilling: true, round, expectedSlot)
                    };

                    var extraction = await _intelligence.ProcessAsync(extractRequest, ct);
                    resolutionLayer = extraction.CamadaResolucao;
                    context.MergeFrom(extraction.DadosExtraidos);
                    ApplyDeterministicSlotExtraction(context, expectedSlot, answer);

                    _logger.LogDebug(
                        "Slot-filling round={Round} Session={SessionId} — " +
                        "VisitorName={V} ResidentName={R} Apartment={A} Block={B} Tower={T} Document={D}",
                        round, call.SessionId,
                        context.VisitorName ?? "null",
                        context.ResidentName ?? "null",
                        context.Apartment ?? "null",
                        context.Block ?? (tenant.UsaBloco ? "null" : "N/A"),
                        context.Tower ?? (tenant.UsaTorre ? "null" : "N/A"),
                        context.Document ?? "null");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Falha na extração de slots round={Round} Session={SessionId}. Prosseguindo.",
                        round,
                        call.SessionId);
                }
                finally
                {
                    llmTimer.Stop();
                    llmProcessingTimeMs = llmTimer.ElapsedMilliseconds;
                }
            }

            var normalizedAnswer = NormalizeUserInput(answer);


                // === ANTI-LOOP DETECTION ===
                // Se a extração NÃO trouxe mudanças, rastreia tentativa e detecta loop
                if (!string.IsNullOrWhiteSpace(answer) && !context.HasChangedSinceLastMerge)
                {
                    var trackedAnswer = normalizedAnswer ?? answer;
                    var isLoopDetected = context.TrackExtractionAttempt(promptToUse ?? "Unknown", trackedAnswer);
                
                    if (isLoopDetected || context.IsLoopDetected)
                    {
                        _logger.LogInformation(
                            "LOOP DETECTADO Session={SessionId} Round={Round} Prompt='{Prompt}' Answer='{Answer}'",
                            call.SessionId, round, promptToUse, trackedAnswer);

                        // Tenta confirmação implícita antes de escalar
                        var implicitConfirmation = context.GetConfirmationPrompt();
                        if (!string.IsNullOrWhiteSpace(implicitConfirmation))
                        {
                            _logger.LogInformation(
                                "Usando confirmação implícita Session={SessionId}: '{Message}'",
                                call.SessionId, implicitConfirmation);
                            // A próxima rodada usará esta pergunta de confirmação
                            // Sistema automaticamente vai usar GetConfirmationPrompt quando GetNextPrompt retorna null
                        }
                        else
                        {
                            // Não há confirmação possível - encerra loop
                            _logger.LogWarning(
                                  "Nenhuma confirmação implícita disponível Session={SessionId}. Encerrando slot-filling.",
                                  call.SessionId);
                            break; // Sai do loop - vai resultar em escalação se não completado
                        }
                    }
                }

                // === VALIDAÇÃO & NORMALIZAÇÃO ===
                if (string.IsNullOrWhiteSpace(normalizedAnswer) && !string.IsNullOrWhiteSpace(answer))
                {
                    _logger.LogWarning(
                        "Entrada normalizada para vazio Session={SessionId} Round={Round} RawInput='{RawInput}' " +
                        "(indicador de sarcasmo/teste)",
                        call.SessionId, round, answer);
                }

            var promptToPersist = round == 0
                ? string.Join(Environment.NewLine, new[] { greetingMessage, promptToUse ?? nextPrompt }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : promptToUse ?? nextPrompt;

            await _callSessionRepository.InsertInteractionAsync(
                new CallInteraction
                {
                    SessionId = call.SessionId,
                    InteractionOrder = round + 1,
                    BotPrompt = promptToPersist,
                    UserTranscription = normalizedAnswer ?? answer ?? string.Empty,
                    ResolutionLayer = resolutionLayer,
                    ExtractedDataJson = SerializeDataOrNull(BuildVisitContextSnapshot(context)),
                    InteractionDurationMs = capture.InteractionDurationMs,
                    LlmProcessingTimeMs = llmProcessingTimeMs,
                    CreatedAt = DateTime.UtcNow
                },
                ct);
        }

        call.VisitContext = context;
        call.EscalateDueToSilence = !context.IsComplete && consecutiveSilentRounds >= maxConsecutiveSilentRounds;

        return new VisitorInputCollectionResult(
            string.Join(Environment.NewLine, conversation.Where(text => !string.IsNullOrWhiteSpace(text))),
            null,
            0,
            true);
    }

    private async Task RegisterCollectedInputInteractionIfNeededAsync(
        AgiCallContext call,
        VisitorInputCollectionResult visitorInput,
        InferenceResponseDto iaResponse,
        CancellationToken ct)
    {
        if (visitorInput.AlreadyPersisted || string.IsNullOrWhiteSpace(visitorInput.Prompt))
            return;

        var nextOrder = await _callSessionRepository.GetNextInteractionOrderAsync(call.SessionId, ct);
        await _callSessionRepository.InsertInteractionAsync(
            new CallInteraction
            {
                SessionId = call.SessionId,
                InteractionOrder = nextOrder,
                BotPrompt = visitorInput.Prompt,
                UserTranscription = visitorInput.Texto ?? string.Empty,
                ResolutionLayer = iaResponse.CamadaResolucao,
                ExtractedDataJson = SerializeDataOrNull(iaResponse.DadosExtraidos),
                InteractionDurationMs = visitorInput.InteractionDurationMs,
                LlmProcessingTimeMs = 0,
                CreatedAt = DateTime.UtcNow
            },
            ct);
    }

    private async Task RegisterFinalInteractionIfNeededAsync(
        AgiCallContext call,
        InferenceResponseDto? iaResponse,
        string acaoExecutada,
        string finalAuditJson,
        CancellationToken ct)
    {
        var nextOrder = await _callSessionRepository.GetNextInteractionOrderAsync(call.SessionId, ct);

        await _callSessionRepository.InsertInteractionAsync(
            new CallInteraction
            {
                SessionId = call.SessionId,
                InteractionOrder = nextOrder,
                BotPrompt = $"Atendimento encerrado. Ação final: {acaoExecutada}.",
                UserTranscription = null,
                ResolutionLayer = iaResponse?.CamadaResolucao,
                ExtractedDataJson = finalAuditJson,
                InteractionDurationMs = 0,
                LlmProcessingTimeMs = 0,
                CreatedAt = DateTime.UtcNow
            },
            ct);
    }

    private async Task<InteractionCaptureResult> AskAndCaptureAsync(AgiCallContext call, string prompt, CancellationToken ct)
    {
        await PlayPromptAsync(call, prompt, ct);

        var interactionTimer = Stopwatch.StartNew();
        var safeUniqueId = call.UniqueId.Replace('.', '-');
        var basePath = Path.Combine("/tmp", $"agi-{safeUniqueId}-{Guid.NewGuid():N}");
        var record = await call.VoiceChannel!.RecordAsync(basePath, maxTimeMs: 7000, ct);

        var transcript = await _stt.TranscribeAsync(basePath + ".wav", call.PreTranscribedText, ct);
        if (string.IsNullOrWhiteSpace(transcript) && record.DigitPressed is not null)
            transcript = $"DTMF:{record.DigitPressed}";

        interactionTimer.Stop();
        call.AudioFilePath = basePath + ".wav";
        return new InteractionCaptureResult(transcript, interactionTimer.ElapsedMilliseconds);
    }

    private async Task PlayPromptAsync(AgiCallContext call, string prompt, CancellationToken ct)
    {
        var audioFile = await _tts.SynthesizeAsync(prompt, ct);
        await call.VoiceChannel!.PlayAsync(audioFile, ct);
    }

    private string BuildFinalAuditJson(
        AgiCallContext call,
        Tenant? tenant,
        InferenceResponseDto? iaResponse,
        string acaoExecutada,
        string respostaFalada)
    {
        var webhookPayload = BuildWebhookPayload(call, tenant, iaResponse);
        object? despachoFinal = null;

        if (webhookPayload is not null)
        {
            despachoFinal = new
            {
                tipo = "webhook",
                destino = tenant?.WebhookUrl,
                sucesso = string.Equals(acaoExecutada, "NOTIFICAR_MORADOR", StringComparison.OrdinalIgnoreCase),
                payload = webhookPayload
            };
        }
        else if (string.Equals(acaoExecutada, "ESCALAR_HUMANO", StringComparison.OrdinalIgnoreCase))
        {
            despachoFinal = new
            {
                tipo = "central",
                destino = tenant?.RamalTransfHumano,
                origem = iaResponse?.AcaoSugerida
            };
        }
        else if (string.Equals(acaoExecutada, "ABRIR_PORTAO", StringComparison.OrdinalIgnoreCase))
        {
            despachoFinal = new
            {
                tipo = "painel_acesso",
                comando = "#9"
            };
        }

        return JsonSerializer.Serialize(new
        {
            camadaResolucao = iaResponse?.CamadaResolucao,
            dadosExtraidos = iaResponse?.DadosExtraidos,
            acaoFinal = acaoExecutada,
            respostaFalada,
            despachoFinal
        });
    }

    private static object? BuildWebhookPayload(AgiCallContext call, Tenant? tenant, InferenceResponseDto? iaResponse)
    {
        if (tenant is null || iaResponse is null)
            return null;

        if (!string.Equals(iaResponse.AcaoSugerida, "NOTIFICAR_MORADOR", StringComparison.OrdinalIgnoreCase))
            return null;

        return new
        {
            tenantId = tenant.Id,
            tenantPid = tenant.Pid,
            sessionId = call.SessionId,
            uniqueId = call.UniqueId,
            callerId = call.CallerId,
            acao = iaResponse.AcaoSugerida,
            resposta = iaResponse.RespostaTexto,
            dados = iaResponse.DadosExtraidos
        };
    }

    private static DadosExtraidosDto BuildVisitContextSnapshot(VisitContext context)
    {
        return new DadosExtraidosDto
        {
            NomeVisitante = context.VisitorName,
            Nome = context.ResidentName,
            Unidade = context.Apartment,
            Bloco = context.Block,
            Torre = context.Tower,
            Documento = context.Document,
            TemDadosExtraidos = !string.IsNullOrWhiteSpace(context.VisitorName)
                || !string.IsNullOrWhiteSpace(context.ResidentName)
                || !string.IsNullOrWhiteSpace(context.Apartment)
                || !string.IsNullOrWhiteSpace(context.Block)
                || !string.IsNullOrWhiteSpace(context.Tower)
                || !string.IsNullOrWhiteSpace(context.Document)
        };
    }

    private static string? SerializeDataOrNull<T>(T? value)
    {
        if (value is null)
            return null;

        return JsonSerializer.Serialize(value);
    }

    private static string FormatConversationTurn(int round, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return $"Rodada {round}: [sem resposta]";

        return $"Rodada {round}: {answer}";
    }

    private static string MapTenantType(string tipoLocal) =>
        tipoLocal.ToUpperInvariant() switch
        {
            "HOSPITALAR" => "hospital",
            "COMERCIAL" => "corporate",
            "INDUSTRIAL" => "logistics",
            _ => "residential"
        };

    private static string GetPeriodGreeting()
    {
        var hour = DateTime.Now.Hour;

        if (hour < 12)
            return "Bom dia";

        if (hour < 18)
            return "Boa tarde";

        return "Boa noite";
    }

    private static string BuildGreetingMessage(Tenant tenant, TenantResponsesDto responses)
    {
        var baseGreeting = $"{GetPeriodGreeting()}. {responses.Saudacao}".Trim();
        if (string.IsNullOrWhiteSpace(tenant.NomeIdentificador))
            return baseGreeting;

        return $"{baseGreeting} Você está falando com {tenant.NomeIdentificador}.";
    }

    private static Dictionary<string, string> BuildInferenceMetadata(
        AgiCallContext call,
        Tenant tenant,
        bool slotFilling = false,
        int? round = null,
        string? expectedSlot = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["callerId"] = call.CallerId,
            ["channel"] = call.Channel,
            ["inputSource"] = call.InputSource,
            ["tenantPid"] = tenant.Pid.ToString(CultureInfo.InvariantCulture),
            ["tenantAiProfile"] = tenant.AiProfile
        };

        if (tenant.AiRegexConfidenceThreshold.HasValue)
            metadata["tenantRegexThreshold"] = tenant.AiRegexConfidenceThreshold.Value.ToString(CultureInfo.InvariantCulture);

        if (tenant.AiNlpConfidenceThreshold.HasValue)
            metadata["tenantNlpThreshold"] = tenant.AiNlpConfidenceThreshold.Value.ToString(CultureInfo.InvariantCulture);

        if (tenant.AiGlobalConfidenceThreshold.HasValue)
            metadata["tenantGlobalThreshold"] = tenant.AiGlobalConfidenceThreshold.Value.ToString(CultureInfo.InvariantCulture);

        if (slotFilling)
            metadata["slotFilling"] = "true";

        if (round.HasValue)
            metadata["round"] = round.Value.ToString(CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(expectedSlot))
            metadata["expectedSlot"] = expectedSlot;

        return metadata;
    }

    private static string GetExpectedSlot(VisitContext context)
    {
        if (string.IsNullOrWhiteSpace(context.VisitorName))
            return "VisitorName";

        if (string.IsNullOrWhiteSpace(context.ResidentName))
            return "ResidentName";

        if (string.IsNullOrWhiteSpace(context.Apartment))
            return "Apartment";

        if (context.RequiresBlock && string.IsNullOrWhiteSpace(context.Block))
            return "Block";

        if (context.RequiresTower && string.IsNullOrWhiteSpace(context.Tower))
            return "Tower";

        return "Document";
    }

    private static void ApplyDeterministicSlotExtraction(VisitContext context, string expectedSlot, string? answer)
    {
        var normalizedAnswer = NormalizeUserInput(answer);
        if (string.IsNullOrWhiteSpace(normalizedAnswer))
            return;

        DadosExtraidosDto? extracted = expectedSlot switch
        {
            "Apartment" => ExtractApartmentData(normalizedAnswer),
            "Block" => ExtractSingleTokenSlot(normalizedAnswer, "Block"),
            "Tower" => ExtractSingleTokenSlot(normalizedAnswer, "Tower"),
            "Document" => ExtractDocumentData(normalizedAnswer),
            _ => null
        };

        if (extracted is not null)
            context.MergeFrom(extracted);
    }

    private static DadosExtraidosDto? ExtractApartmentData(string answer)
    {
        var block = ExtractPatternValue(BlockPattern, answer);
        var tower = ExtractPatternValue(TowerPattern, answer);
        var apartment = ExtractPatternValue(ApartmentKeywordPattern, answer);

        if (string.IsNullOrWhiteSpace(apartment))
        {
            var stripped = BlockPattern.Replace(answer, " ");
            stripped = TowerPattern.Replace(stripped, " ");
            stripped = FillerWordsPattern.Replace(stripped, " ");

            var leadingToken = ExtractLeadingBareToken(stripped);
            if (!string.IsNullOrWhiteSpace(leadingToken))
                apartment = leadingToken;
        }

        if (string.IsNullOrWhiteSpace(apartment) && string.IsNullOrWhiteSpace(block) && string.IsNullOrWhiteSpace(tower))
            return null;

        return new DadosExtraidosDto
        {
            Unidade = apartment,
            Bloco = block,
            Torre = tower,
            TemDadosExtraidos = true
        };
    }

    private static DadosExtraidosDto? ExtractSingleTokenSlot(string answer, string slotName)
    {
        var explicitValue = slotName switch
        {
            "Block" => ExtractPatternValue(BlockPattern, answer),
            "Tower" => ExtractPatternValue(TowerPattern, answer),
            _ => null
        };

        var value = explicitValue ?? ExtractLeadingBareToken(answer);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return slotName switch
        {
            "Block" => new DadosExtraidosDto { Bloco = value, TemDadosExtraidos = true },
            "Tower" => new DadosExtraidosDto { Torre = value, TemDadosExtraidos = true },
            _ => null
        };
    }

    private static DadosExtraidosDto? ExtractDocumentData(string answer)
    {
        if (!TryExtractDocumentDigits(answer, out var document))
            return null;

        return new DadosExtraidosDto
        {
            Documento = document,
            Cpf = document.Length == 11 ? document : null,
            TemDadosExtraidos = true
        };
    }

    private static string? ExtractPatternValue(Regex pattern, string answer)
    {
        var match = pattern.Match(answer);
        if (!match.Success)
            return null;

        var value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractLeadingBareToken(string answer)
    {
        var tokens = answer
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        if (tokens.Length == 0)
            return null;

        var candidate = tokens[0];
        if (tokens.Length > 1 && tokens.All(token => string.Equals(token, candidate, StringComparison.OrdinalIgnoreCase)))
            return BareSlotTokenPattern.IsMatch(candidate) ? candidate : null;

        return BareSlotTokenPattern.IsMatch(candidate) ? candidate : null;
    }

    private static bool TryExtractDocumentDigits(string answer, out string document)
    {
        document = string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
            return false;

        var spokenDigitMap = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = '0',
            ["um"] = '1',
            ["uma"] = '1',
            ["dois"] = '2',
            ["duas"] = '2',
            ["tres"] = '3',
            ["três"] = '3',
            ["quatro"] = '4',
            ["cinco"] = '5',
            ["seis"] = '6',
            ["sete"] = '7',
            ["oito"] = '8',
            ["nove"] = '9'
        };

        var digits = new List<char>(answer.Length);
        foreach (var token in answer.Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.All(char.IsDigit))
            {
                digits.AddRange(token);
                continue;
            }

            if (spokenDigitMap.TryGetValue(token, out var digit))
                digits.Add(digit);
        }

        if (digits.Count < 6)
            return false;

        document = new string(digits.ToArray());
        return true;
    }

    /// <summary>
    /// Valida se a transcrição do usuário contém padrões suspeitos indicando invalidade ou sarcasmo.
    /// Exemplos: "um milhão", "3 milhões", etc.
    /// Retorna <c>true</c> se a entrada é suspeita.
    /// </summary>
    private static bool IsSuspiciousInput(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return false;

        var lowerTranscript = transcript.ToLowerInvariant().Trim();

        // Detecta padrões de números enormes em português (sarcasmo/teste)
        if (lowerTranscript.Contains("milhão") || lowerTranscript.Contains("bilhão") || 
            lowerTranscript.Contains("trilhão") || lowerTranscript.Contains("mil mil"))
            return true;

        return false;
    }

    /// <summary>
    /// Detecta se a entrada é uma repetição excessiva do mesmo valor.
    /// Exemplo: "204 204 204" ou "um dois três um dois três" repetido 2+ vezes.
    /// </summary>
    private static bool IsRepetitiveInput(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return false;

        var words = transcript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3)
            return false;

        // Se tem 3+ palavras e todas são iguais, é repetição
        var firstWord = words[0];
        return words.All(w => w.Equals(firstWord, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normaliza a entrada do usuário removendo espaços excessivos e detectando
    /// padrões de invalidade. Retorna a entrada normalizada ou uma string vazia
    /// se detectar invalidade/sarcasmo.
    /// </summary>
    private static string NormalizeUserInput(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return string.Empty;

        // Detecta sarcasmo/teste antes de processar
        if (IsSuspiciousInput(transcript))
        {
            // Log a suspeita mas retorna vazio para o sistema reprocessar/escalar
            return string.Empty;
        }

        // Detecta repetição excessiva
        if (IsRepetitiveInput(transcript))
        {
            // Retorna apenas o primeiro token para análise clean
            var words = transcript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return words.FirstOrDefault() ?? string.Empty;
        }

        // Remove espaços múltiplos e normaliza
        var normalized = System.Text.RegularExpressions.Regex.Replace(transcript.Trim(), @"\s+", " ");
        return normalized;
    }

    /// <summary>
    /// Detecta qual slot primário o usuário estava tentando informar
    /// baseado na transcrição. Retorna "Unidade", "Bloco", "Torre", "Documento", etc.
    /// </summary>
    private static string? IdentifyPrimarySlot(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return null;

        var lowerTranscript = transcript.ToLowerInvariant();

        // Palavras-chave para identificar slots
        if (lowerTranscript.Contains("bloco"))
            return "Bloco";
        if (lowerTranscript.Contains("torre"))
            return "Torre";
        if (lowerTranscript.Contains("apertar") || lowerTranscript.Contains("ramal"))
            return "Apartamento";
        if (lowerTranscript.Contains("cpf") || lowerTranscript.Contains("rg") || lowerTranscript.Contains("documento"))
            return "Documento";
        if (lowerTranscript.Contains("empresa"))
            return "Empresa";

        return null;
    }

    private sealed record InteractionCaptureResult(string? Transcript, long InteractionDurationMs);
    private sealed record VisitorInputCollectionResult(string? Texto, string? Prompt, long InteractionDurationMs, bool AlreadyPersisted);
}
