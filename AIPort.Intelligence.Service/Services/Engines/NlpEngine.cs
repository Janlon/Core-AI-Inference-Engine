using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text.Json;
using Catalyst;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;
using Mosaik.Core;

namespace AIPort.Intelligence.Service.Services.Engines;

public sealed partial class NlpEngine : INlpProcessor
{
    private readonly IOptions<AIServiceOptions> _options;
    private readonly ILogger<NlpEngine> _logger;
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static Pipeline? _pipeline;
    private static bool _initAttempted;

    [GeneratedRegex(@"(?<![a-zA-Z])([A-Z][a-z]{2,}(?:\s+(?:de\s+|da\s+|do\s+)?[A-Z][a-z]{2,}){0,3})",
        RegexOptions.Compiled)]
    private static partial Regex NomeProprio();

    [GeneratedRegex(@"\b(?:me\s+chamo|meu\s+nome\s+[eé]|eu\s+sou)\s+([A-ZÁÀÂÃÉÊÍÓÔÕÚÇ][a-záàâãéêíóôõúç]{1,30})(?=\s*[,\.!\?;:]|\s|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeAutoDeclaradoPattern();

    // SEM IgnoreCase: sufixos juridicos BR sao uppercase (S.A., LTDA, ME, EPP).
    // Com IgnoreCase, "nome" casava com o sufixo "ME" gerando empresa falsa.
    [GeneratedRegex(@"\b([A-Z][A-Za-z\s&\.]{3,40}(?:S\.A\.|S/A|LTDA\.?|EIRELI\.?|EPP\.?|ME\b))\b",
        RegexOptions.Compiled)]
    private static partial Regex CnpjEmpresa();

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Boa", "Bom", "Oi", "Ola", "Dia", "Tarde", "Noite", "Sim", "Nao", "Beleza", "Joia", "Baum", "Bao", "Bão",
        "Obrigado", "Obrigada", "Por", "Favor", "Gostaria", "Quero", "Preciso",
        "Estou", "Venho", "Sou", "Falar", "Visitar", "Ver", "Apartamento", "Sala",
        "Meu", "Minha", "Meus", "Minhas", "Nome", "Voce", "Seria", "Tenho",
        "Bloco", "Andar", "Piso", "Numero"
    };

    private static readonly HashSet<string> InvalidSingleWordNameCandidates = new(StringComparer.OrdinalIgnoreCase)
    {
        "oi", "ola", "olá", "opa", "fala", "salve", "eai", "e", "aí", "ei", "beleza", "joia", "jóia", "baum", "bao", "bão",
        "bom", "boa", "dia", "tarde", "noite", "obrigado", "obrigada", "sim", "nao", "não",
        "entregador", "motoboy", "pedido", "entrega", "ifood"
    };

    private static readonly HashSet<string> NonNameTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "oi", "ola", "olá", "opa", "fala", "salve", "eai", "e", "aí", "ei", "beleza", "joia", "jóia", "baum", "bao", "bão",
        "bom", "boa", "dia", "tarde", "noite", "obrigado", "obrigada", "sim", "nao", "não",
        "quero", "falar", "com", "visitar", "ver", "sou", "me", "chamo", "meu", "nome",
        "aqui", "e", "é", "vim", "venho", "preciso", "para", "pra", "no", "na", "ao",
        "o", "a", "os", "as", "um", "uma", "entregador", "motoboy", "pedido", "entrega", "ifood"
    };

    private static readonly HashSet<string> TrailingContextTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "na", "no", "em", "para", "pra", "ao", "a", "o", "bloco", "torre", "apartamento", "apto", "unidade"
    };

    public NlpEngine(IOptions<AIServiceOptions> options, ILogger<NlpEngine> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<ProcessingLayerResult> ProcessAsync(
        string texto,
        ProcessingState estadoAtual,
        CancellationToken ct = default)
    {
        if (!_options.Value.Nlp.Enabled)
        {
            _logger.LogDebug("Camada NLP desabilitada por configuracao.");
            return Task.FromResult(BuildFromState(estadoAtual, 0.0));
        }

        return ProcessCoreAsync(texto, estadoAtual, ct);
    }

    private async Task<ProcessingLayerResult> ProcessCoreAsync(
        string texto,
        ProcessingState estadoAtual,
        CancellationToken ct)
    {
        string? nome = estadoAtual.NomeDetectado;
        string? empresa = estadoAtual.EmpresaDetectada;
        double confianca = estadoAtual.MelhorConfianca;
        int extractions = 0;
        var provider = "NLP-Heuristic";

        // Camada preferencial: spaCy via processo Python externo (se habilitado)
        if (_options.Value.Nlp.UseSpacy)
        {
            var spacy = await TryProcessWithSpacyAsync(texto, ct);
            if (spacy.Success)
            {
                if (nome is null && !string.IsNullOrWhiteSpace(spacy.Nome))
                {
                    nome = spacy.Nome;
                    extractions++;
                }

                if (empresa is null && !string.IsNullOrWhiteSpace(spacy.Empresa))
                {
                    empresa = spacy.Empresa;
                    extractions++;
                }

                if (extractions > 0)
                    provider = "NLP-spaCy";
            }
        }

        var catalystLoaded = false;

        if (_options.Value.Nlp.UseCatalyst)
            catalystLoaded = await EnsureCatalystPipelineAsync(ct);

        if (catalystLoaded && (nome is null || empresa is null))
        {
            try
            {
                var language = ResolveLanguage(_options.Value.Nlp.ModelLanguage);
                var doc = new Document(texto, language);
                _pipeline!.ProcessSingle(doc);

                foreach (var span in doc.Spans)
                {
                    var label = TryGetSpanLabel(span);
                    var value = TryGetSpanValue(span);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    if (nome is null && IsPersonLabel(label))
                    {
                        nome = value;
                        extractions++;
                        provider = "NLP-Catalyst";
                    }

                    if (empresa is null && IsOrganizationLabel(label))
                    {
                        empresa = value;
                        extractions++;
                        provider = "NLP-Catalyst";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalyst falhou em runtime; usando fallback heuristico.");
            }
        }


        // Fallback heurístico para nomes: padrões explícitos, início de frase, nomes simples
        if (nome is null)
        {
            var nomeAuto = NomeAutoDeclaradoPattern().Match(texto);
            if (nomeAuto.Success)
            {
                var candidato = NormalizeNameCandidate(nomeAuto.Groups[1].Value);
                if (IsValidPersonNameCandidate(candidato))
                {
                    nome = candidato;
                    extractions++;
                }
            }
        }

        if (nome is null)
        {
            foreach (Match m in NomeProprio().Matches(texto))
            {
                var candidato = NormalizeNameCandidate(m.Groups[1].Value);
                if (!StopWords.Contains(candidato.Split(' ')[0])
                    && candidato.Split(' ').Length >= 2
                    && IsValidPersonNameCandidate(candidato))
                {
                    nome = candidato;
                    extractions++;
                    break;
                }
            }
        }

        // Novo: nome no início da frase (fallback para casos simples)
        if (nome is null)
        {
            var palavras = texto.Trim().Split(' ');
            if (palavras.Length > 0)
            {
                var primeira = NormalizeNameCandidate(palavras[0].Trim());
                if (primeira.Length > 1
                    && char.IsUpper(primeira[0])
                    && primeira.All(char.IsLetter)
                    && IsValidPersonNameCandidate(primeira))
                {
                    nome = primeira;
                    extractions++;
                }
            }
        }

        // Novo: nome simples em resposta direta
        if (nome is null)
        {
            var match = Regex.Match(texto.Trim(), @"^([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var candidato = NormalizeNameCandidate(match.Groups[1].Value);
                if (IsValidPersonNameCandidate(candidato))
                {
                    nome = candidato;
                    extractions++;
                }
            }
        }

        if (empresa is null)
        {
            var empMatch = CnpjEmpresa().Match(texto);
            if (empMatch.Success)
            {
                empresa = empMatch.Groups[1].Value.Trim();
                extractions++;
            }
        }

        if (extractions > 0)
            confianca = Math.Max(confianca, 0.60 + (extractions * 0.10));

        var intencao = estadoAtual.Intencao
            ?? (nome is not null || empresa is not null ? Intencao.Identificacao : (Intencao?)null);

        _logger.LogDebug("NLP layer: extracoes={E}, confianca={C:P0}, nome={N}, provider={Provider}, catalyst={Catalyst}",
            extractions, confianca, nome, provider, catalystLoaded);

        return new ProcessingLayerResult
        {
            Camada = provider,
            Confianca = confianca,
            Intencao = intencao,
            DadosExtraidos = new()
            {
                Nome = nome,
                NomeVisitante = estadoAtual.NomeVisitanteDetectado,
                Documento = estadoAtual.DocumentoDetectado,
                Cpf = estadoAtual.CpfDetectado,
                Unidade = estadoAtual.UnidadeDetectada,
                Bloco = estadoAtual.BlocoDetectado,
                Torre = estadoAtual.TorreDetectada,
                Empresa = empresa,
                Parentesco = estadoAtual.ParentescoDetectado,
                EstaComVeiculo = estadoAtual.EstaComVeiculoDetectado ?? false,
                Placa = estadoAtual.PlacaDetectada,
                EEntregador = estadoAtual.EEntregadorDetectado ?? false
            }
        };
    }

    private async Task<bool> EnsureCatalystPipelineAsync(CancellationToken ct)
    {
        if (_pipeline is not null)
            return true;

        if (_initAttempted)
            return false;

        await InitLock.WaitAsync(ct);
        try
        {
            if (_pipeline is not null)
                return true;

            if (_initAttempted)
                return false;

            _initAttempted = true;

            var storagePath = ResolveCatalystStoragePath();
            Directory.CreateDirectory(storagePath);
            Storage.Current = new DiskStorage(storagePath);

            var language = ResolveLanguage(_options.Value.Nlp.ModelLanguage);
            var hadLocalModels = HasLocalCatalystModels(storagePath);

            // Pipeline.ForAsync faz lazy-load dos modelos no DiskStorage.
            // Sem modelos locais, tentará obter do repositório online do Catalyst.
            _pipeline = await Pipeline.ForAsync(language);

            if (hadLocalModels)
            {
                _logger.LogInformation(
                    "Catalyst carregado com sucesso para idioma {Lang} usando modelos locais em {Path}.",
                    language,
                    storagePath);
            }
            else
            {
                _logger.LogInformation(
                    "Catalyst carregado para idioma {Lang}. Modelos locais não foram encontrados e foi tentado auto-download para {Path}.",
                    language,
                    storagePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel inicializar Catalyst. NLP seguira com fallback heuristico.");
            return false;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static string ResolveCatalystStoragePath()
    {
        // Usa caminho absoluto para evitar diferenças de cwd em publish single-file/systemd.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "catalyst-models"));
    }

    private static bool HasLocalCatalystModels(string storagePath)
    {
        var modelsRoot = Path.Combine(storagePath, "Models");
        if (!Directory.Exists(modelsRoot))
            return false;

        return Directory.EnumerateFiles(modelsRoot, "*", SearchOption.AllDirectories).Any();
    }

    private static Language ResolveLanguage(string languageCode)
    {
        return languageCode.Trim().ToLowerInvariant() switch
        {
            "pt" or "pt-br" or "portuguese" => Language.Portuguese,
            _ => Language.English
        };
    }

    private static bool IsPersonLabel(string label)
    {
        var normalized = label.Trim().ToUpperInvariant();
        return normalized is "PER" or "PERSON" or "PESSOA";
    }

    private static bool IsValidPersonNameCandidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var tokens = candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim(',', '.', ';', ':', '!', '?', '"', '\''))
            .Where(token => token.Length > 0)
            .ToArray();

        if (tokens.Length == 0)
            return false;

        if (tokens.Length == 1 && InvalidSingleWordNameCandidates.Contains(tokens[0]))
            return false;

        if (tokens.All(token => NonNameTokens.Contains(token)))
            return false;

        return true;
    }

    private static string NormalizeNameCandidate(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        var tokens = candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim(',', '.', ';', ':', '!', '?', '"', '\''))
            .Where(token => token.Length > 0)
            .ToList();

        while (tokens.Count > 0 && TrailingContextTokens.Contains(tokens[^1]))
            tokens.RemoveAt(tokens.Count - 1);

        return string.Join(' ', tokens);
    }

    private static bool IsOrganizationLabel(string label)
    {
        var normalized = label.Trim().ToUpperInvariant();
        return normalized is "ORG" or "ORGANIZATION" or "EMPRESA";
    }

    private static string TryGetSpanLabel(object span)
    {
        var type = span.GetType();

        var labelProp = type.GetProperty("Label") ?? type.GetProperty("Tag") ?? type.GetProperty("EntityType") ?? type.GetProperty("Type");
        var label = labelProp?.GetValue(span)?.ToString();
        return label ?? string.Empty;
    }

    private static string? TryGetSpanValue(object span)
    {
        var type = span.GetType();

        var valueProp = type.GetProperty("Value") ?? type.GetProperty("Text") ?? type.GetProperty("Token");
        var value = valueProp?.GetValue(span)?.ToString();
        return value?.Trim();
    }

    private async Task<SpacyResult> TryProcessWithSpacyAsync(string texto, CancellationToken ct)
    {
        var py = _options.Value.Nlp.SpacyPythonExecutable;
        var model = _options.Value.Nlp.SpacyModel;
        var timeoutMs = Math.Max(1000, _options.Value.Nlp.SpacyTimeoutMs);

        var psi = new ProcessStartInfo
        {
            FileName = py,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(SpacyScript);
        psi.ArgumentList.Add(model);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            await process.StandardInput.WriteAsync(texto);
            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            var waitTask = process.WaitForExitAsync(timeoutCts.Token);
            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await Task.WhenAll(waitTask, outputTask, errTask);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("spaCy retornou exit code {Code}. stderr={Err}", process.ExitCode, errTask.Result);
                return SpacyResult.Empty;
            }

            return ParseSpacyResult(outputTask.Result);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            _logger.LogWarning("spaCy timeout apos {TimeoutMs}ms", timeoutMs);
            return SpacyResult.Empty;
        }
        catch (Exception ex)
        {
            TryKill(process);
            _logger.LogWarning(ex, "Falha ao invocar spaCy via Python.");
            return SpacyResult.Empty;
        }
    }

    private static SpacyResult ParseSpacyResult(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return SpacyResult.Empty;

        try
        {
            using var json = JsonDocument.Parse(stdout);
            var root = json.RootElement;
            var nome = root.TryGetProperty("nome", out var n) ? n.GetString() : null;
            var empresa = root.TryGetProperty("empresa", out var e) ? e.GetString() : null;
            var ok = root.TryGetProperty("ok", out var o) && o.GetBoolean();

            return new SpacyResult(ok, nome, empresa);
        }
        catch
        {
            return SpacyResult.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // noop
        }
    }

    private const string SpacyScript =
        "import sys, json, spacy\n"
        + "model = sys.argv[1] if len(sys.argv) > 1 else 'pt_core_news_sm'\n"
        + "text = sys.stdin.read()\n"
        + "out = {'ok': False, 'nome': None, 'empresa': None}\n"
        + "try:\n"
        + "    nlp = spacy.load(model)\n"
        + "    doc = nlp(text)\n"
        + "    for ent in doc.ents:\n"
        + "        label = ent.label_.upper()\n"
        + "        value = ent.text.strip()\n"
        + "        if not value:\n"
        + "            continue\n"
        + "        if out['nome'] is None and label in ('PER','PERSON'):\n"
        + "            out['nome'] = value\n"
        + "        elif out['empresa'] is None and label in ('ORG','ORGANIZATION'):\n"
        + "            out['empresa'] = value\n"
        + "    out['ok'] = True\n"
        + "except Exception:\n"
        + "    out['ok'] = False\n"
        + "print(json.dumps(out, ensure_ascii=False))";

    private readonly record struct SpacyResult(bool Success, string? Nome, string? Empresa)
    {
        public static SpacyResult Empty => new(false, null, null);
    }

    private static ProcessingLayerResult BuildFromState(ProcessingState state, double confianca) => new()
    {
        Camada = "NLP",
        Confianca = confianca,
        Intencao = state.Intencao,
        DadosExtraidos = state.ToDadosExtraidos()
    };
}
