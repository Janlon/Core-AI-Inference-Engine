using AIPort.Adapter.Orchestrator.Data.Entities;
using AIPort.Adapter.Orchestrator.Data.Repositories;
using AIPort.Adapter.Orchestrator.Domain.Abstractions;
using AIPort.Adapter.Orchestrator.Domain.Models;
using AIPort.Adapter.Orchestrator.Integrations.Interfaces;
using AIPort.Adapter.Orchestrator.Services;
using AIPort.Adapter.Orchestrator.Services.Interfaces;
using System.Collections.Generic;

namespace AIPort.Adapter.Orchestrator.Tests.Services;

public sealed class OrchestrationServiceSilenceGuardTests
{
    [Fact]
    public async Task HandleCallAsync_Residential_FirstSilentTurn_EscalatesToHumanWithoutLooping()
    {
        var tenantRepository = new Mock<ITenantRepository>();
        var callSessionRepository = new Mock<ICallSessionRepository>();
        var stt = new Mock<ISpeechToTextService>();
        var tts = new Mock<ITextToSpeechService>();
        var intelligence = new Mock<IIntelligenceServiceClient>();
        var executor = new Mock<IDecisionExecutor>();
        var eventService = new Mock<IEventService>();
        var voiceChannel = new Mock<IVoiceChannel>();

        var tenant = new Tenant
        {
            Id = 1,
            Pid = 200,
            NomeIdentificador = "Condominio Monte Verde",
            TipoLocal = "RESIDENCIAL",
            SystemType = "condominio",
            IsActive = true,
            UsaBloco = true,
            UsaTorre = false,
            RecordingEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        tenantRepository
            .Setup(x => x.GetByPidAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        intelligence
            .Setup(x => x.GetTenantResponsesAsync("residential", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantResponsesDto());

        voiceChannel
            .Setup(x => x.RecordAsync(It.IsAny<string>(), 7000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VoiceChannelResponse(200, 0, null, "OK"));

        stt
            .Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        tts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dummy.wav");

        callSessionRepository
            .Setup(x => x.CreateSessionAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        callSessionRepository
            .Setup(x => x.InsertInteractionAsync(It.IsAny<CallInteraction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

        callSessionRepository
            .Setup(x => x.GetNextInteractionOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        callSessionRepository
            .Setup(x => x.CompleteSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        InferenceResponseDto? capturedInference = null;
        executor
            .Setup(x => x.ExecuteAsync(It.IsAny<AgiCallContext>(), It.IsAny<Tenant>(), It.IsAny<InferenceResponseDto>(), It.IsAny<CancellationToken>()))
            .Callback<AgiCallContext, Tenant, InferenceResponseDto, CancellationToken>((_, _, inference, _) => capturedInference = inference)
            .ReturnsAsync(("ESCALAR_HUMANO", "transferindo"));

        var sut = new OrchestrationService(
            tenantRepository.Object,
            callSessionRepository.Object,
            stt.Object,
            tts.Object,
            intelligence.Object,
            executor.Object,
            eventService.Object,
            Mock.Of<ILogger<OrchestrationService>>());

        var call = new AgiCallContext
        {
            SessionId = "1775077119.31",
            UniqueId = "1775077119.31",
            CallerId = "100",
            Channel = "PJSIP/100-00000011",
            TenantPid = 200,
            VoiceChannel = voiceChannel.Object
        };

        var result = await sut.HandleCallAsync(call, CancellationToken.None);

        Assert.True(result.Sucesso);
        Assert.Equal("ESCALAR_HUMANO", result.AcaoExecutada);

        Assert.NotNull(capturedInference);
        Assert.Equal("ESCALAR_HUMANO", capturedInference!.AcaoSugerida);
        Assert.Equal("NO_INPUT_GUARD", capturedInference.CamadaResolucao);

        voiceChannel.Verify(x => x.RecordAsync(It.IsAny<string>(), 7000, It.IsAny<CancellationToken>()), Times.Once);
        intelligence.Verify(x => x.ProcessAsync(It.IsAny<InferenceRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleCallAsync_Residential_BlockOnlyTurn_UsesConfirmationPrompt_AndCompletesWithBareApartment()
    {
        var tenantRepository = new Mock<ITenantRepository>();
        var callSessionRepository = new Mock<ICallSessionRepository>();
        var stt = new Mock<ISpeechToTextService>();
        var tts = new Mock<ITextToSpeechService>();
        var intelligence = new Mock<IIntelligenceServiceClient>();
        var executor = new Mock<IDecisionExecutor>();
        var eventService = new Mock<IEventService>();
        var voiceChannel = new Mock<IVoiceChannel>();

        var tenant = CreateResidentialTenant();
        tenantRepository
            .Setup(x => x.GetByPidAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        intelligence
            .Setup(x => x.GetTenantResponsesAsync("residential", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantResponsesDto());

        var transcripts = new Queue<string>(new[]
        {
            "João Carlos da Silva",
            "Giovana",
            "bloco 12",
            "306",
            "Um dois três quatro cinco seis sete oito nove zero zero."
        });

        voiceChannel
            .Setup(x => x.RecordAsync(It.IsAny<string>(), 7000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VoiceChannelResponse(200, 0, null, "OK"));

        stt
            .Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => transcripts.Dequeue());

        tts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dummy.wav");

        var extractionResponses = new Queue<InferenceResponseDto>(new[]
        {
            CreateExtractionResponse(new DadosExtraidosDto { NomeVisitante = "João Carlos da Silva", Nome = "João Carlos da Silva", TemDadosExtraidos = true }),
            CreateExtractionResponse(new DadosExtraidosDto { Nome = "Giovana", TemDadosExtraidos = true }),
            CreateExtractionResponse(new DadosExtraidosDto { Bloco = "12", TemDadosExtraidos = true }),
            CreateExtractionResponse(new DadosExtraidosDto { TemDadosExtraidos = false }),
            CreateExtractionResponse(new DadosExtraidosDto { TemDadosExtraidos = false })
        });

        intelligence
            .Setup(x => x.ProcessAsync(It.IsAny<InferenceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => extractionResponses.Dequeue());

        callSessionRepository
            .Setup(x => x.CreateSessionAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var insertedInteractions = new List<CallInteraction>();
        callSessionRepository
            .Setup(x => x.InsertInteractionAsync(It.IsAny<CallInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<CallInteraction, CancellationToken>((interaction, _) => insertedInteractions.Add(interaction))
            .ReturnsAsync(1L);

        callSessionRepository
            .Setup(x => x.GetNextInteractionOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(6);

        callSessionRepository
            .Setup(x => x.CompleteSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        InferenceResponseDto? capturedInference = null;
        executor
            .Setup(x => x.ExecuteAsync(It.IsAny<AgiCallContext>(), It.IsAny<Tenant>(), It.IsAny<InferenceResponseDto>(), It.IsAny<CancellationToken>()))
            .Callback<AgiCallContext, Tenant, InferenceResponseDto, CancellationToken>((_, _, inference, _) => capturedInference = inference)
            .ReturnsAsync(("NOTIFICAR_MORADOR", "ok"));

        var sut = CreateSut(tenantRepository, callSessionRepository, stt, tts, intelligence, executor, eventService);
        var call = CreateCall(voiceChannel.Object);

        var result = await sut.HandleCallAsync(call, CancellationToken.None);

        Assert.True(result.Sucesso);
        Assert.Equal("NOTIFICAR_MORADOR", result.AcaoExecutada);
        Assert.NotNull(capturedInference);
        Assert.Equal("SLOT_FILLING", capturedInference!.CamadaResolucao);
        Assert.Equal("João Carlos da Silva", capturedInference.DadosExtraidos.NomeVisitante);
        Assert.Equal("Giovana", capturedInference.DadosExtraidos.Nome);
        Assert.Equal("306", capturedInference.DadosExtraidos.Unidade);
        Assert.Equal("12", capturedInference.DadosExtraidos.Bloco);
        Assert.Equal("12345678900", capturedInference.DadosExtraidos.Documento);

        Assert.Contains(insertedInteractions, interaction => interaction.BotPrompt == "Informe o nome do morador que deseja visitar.");
        Assert.Contains(insertedInteractions, interaction => interaction.BotPrompt == "Entendido, você está no bloco 12. Qual o número do apartamento?");
        Assert.Equal(1, insertedInteractions.Count(interaction => interaction.BotPrompt == "Informe o número do apartamento."));
    }

    [Fact]
    public async Task HandleCallAsync_Residential_MixedApartmentAndBlockTurn_CompletesWithoutRepeatingApartmentPrompt()
    {
        var tenantRepository = new Mock<ITenantRepository>();
        var callSessionRepository = new Mock<ICallSessionRepository>();
        var stt = new Mock<ISpeechToTextService>();
        var tts = new Mock<ITextToSpeechService>();
        var intelligence = new Mock<IIntelligenceServiceClient>();
        var executor = new Mock<IDecisionExecutor>();
        var eventService = new Mock<IEventService>();
        var voiceChannel = new Mock<IVoiceChannel>();

        tenantRepository
            .Setup(x => x.GetByPidAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResidentialTenant());

        intelligence
            .Setup(x => x.GetTenantResponsesAsync("residential", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantResponsesDto());

        var transcripts = new Queue<string>(new[]
        {
            "João Carlos da Silva",
            "Giovana",
            "306 bloco 12",
            "Um dois três quatro cinco seis sete oito nove zero zero."
        });

        voiceChannel
            .Setup(x => x.RecordAsync(It.IsAny<string>(), 7000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VoiceChannelResponse(200, 0, null, "OK"));

        stt
            .Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => transcripts.Dequeue());

        tts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dummy.wav");

        var extractionResponses = new Queue<InferenceResponseDto>(new[]
        {
            CreateExtractionResponse(new DadosExtraidosDto { NomeVisitante = "João Carlos da Silva", Nome = "João Carlos da Silva", TemDadosExtraidos = true }),
            CreateExtractionResponse(new DadosExtraidosDto { Nome = "Giovana", TemDadosExtraidos = true }),
            CreateExtractionResponse(new DadosExtraidosDto { TemDadosExtraidos = false }),
            CreateExtractionResponse(new DadosExtraidosDto { TemDadosExtraidos = false })
        });

        intelligence
            .Setup(x => x.ProcessAsync(It.IsAny<InferenceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => extractionResponses.Dequeue());

        callSessionRepository
            .Setup(x => x.CreateSessionAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var insertedInteractions = new List<CallInteraction>();
        callSessionRepository
            .Setup(x => x.InsertInteractionAsync(It.IsAny<CallInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<CallInteraction, CancellationToken>((interaction, _) => insertedInteractions.Add(interaction))
            .ReturnsAsync(1L);

        callSessionRepository
            .Setup(x => x.GetNextInteractionOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        callSessionRepository
            .Setup(x => x.CompleteSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        InferenceResponseDto? capturedInference = null;
        executor
            .Setup(x => x.ExecuteAsync(It.IsAny<AgiCallContext>(), It.IsAny<Tenant>(), It.IsAny<InferenceResponseDto>(), It.IsAny<CancellationToken>()))
            .Callback<AgiCallContext, Tenant, InferenceResponseDto, CancellationToken>((_, _, inference, _) => capturedInference = inference)
            .ReturnsAsync(("NOTIFICAR_MORADOR", "ok"));

        var sut = CreateSut(tenantRepository, callSessionRepository, stt, tts, intelligence, executor, eventService);
        var call = CreateCall(voiceChannel.Object);

        var result = await sut.HandleCallAsync(call, CancellationToken.None);

        Assert.True(result.Sucesso);
        Assert.Equal("NOTIFICAR_MORADOR", result.AcaoExecutada);
        Assert.NotNull(capturedInference);
        Assert.Equal("306", capturedInference!.DadosExtraidos.Unidade);
        Assert.Equal("12", capturedInference.DadosExtraidos.Bloco);
        Assert.Equal(1, insertedInteractions.Count(interaction => interaction.BotPrompt == "Informe o número do apartamento."));
        Assert.DoesNotContain(insertedInteractions, interaction => interaction.BotPrompt == "Entendido, você está no bloco 12. Qual o número do apartamento?");
    }

    private static Tenant CreateResidentialTenant()
    {
        return new Tenant
        {
            Id = 1,
            Pid = 200,
            NomeIdentificador = "Condominio Monte Verde",
            TipoLocal = "RESIDENCIAL",
            SystemType = "condominio",
            IsActive = true,
            UsaBloco = true,
            UsaTorre = false,
            RecordingEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static InferenceResponseDto CreateExtractionResponse(DadosExtraidosDto dados)
    {
        return new InferenceResponseDto
        {
            Intencao = "VISITA_RESIDENCIAL",
            AcaoSugerida = "SOLICITAR_IDENTIFICACAO",
            RespostaTexto = "ok",
            CamadaResolucao = "SlotFilling-FastPath",
            Confianca = 0.65,
            DadosExtraidos = dados
        };
    }

    private static OrchestrationService CreateSut(
        Mock<ITenantRepository> tenantRepository,
        Mock<ICallSessionRepository> callSessionRepository,
        Mock<ISpeechToTextService> stt,
        Mock<ITextToSpeechService> tts,
        Mock<IIntelligenceServiceClient> intelligence,
        Mock<IDecisionExecutor> executor,
        Mock<IEventService> eventService)
    {
        return new OrchestrationService(
            tenantRepository.Object,
            callSessionRepository.Object,
            stt.Object,
            tts.Object,
            intelligence.Object,
            executor.Object,
            eventService.Object,
            Mock.Of<ILogger<OrchestrationService>>());
    }

    private static AgiCallContext CreateCall(IVoiceChannel voiceChannel)
    {
        return new AgiCallContext
        {
            SessionId = "1775077119.31",
            UniqueId = "1775077119.31",
            CallerId = "100",
            Channel = "PJSIP/100-00000011",
            TenantPid = 200,
            VoiceChannel = voiceChannel
        };
    }
}
