using System.Text.RegularExpressions;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AIPort.Intelligence.Service.Services.Engines;

public sealed partial class RegexEngine : IRegexProcessor
{
    private readonly IOptions<AIServiceOptions> _options;
    private readonly ILogger<RegexEngine> _logger;


    // CPF formatado: 123.456.789-01 ou só números: 12345678901
    [GeneratedRegex(@"([0-9]{3}\.?[0-9]{3}\.?[0-9]{3}-?[0-9]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CpfPattern();
    [GeneratedRegex(@"(?:cpf\s*[:=]?\s*)?([0-9]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CpfOnlyDigitsPattern();
    [GeneratedRegex(@"([0-9]{3}\s+[0-9]{3}\s+[0-9]{3}\s+[0-9]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CpfSpacedPattern();
    [GeneratedRegex(@"((?:um|dois|tr[eê]s|quatro|cinco|seis|sete|oito|nove|zero)(?:\s+(?:um|dois|tr[eê]s|quatro|cinco|seis|sete|oito|nove|zero)){10,})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CpfSpokenPattern();
    [GeneratedRegex(@"(?:apartamento|apê|ap|unidade|sala)\s*(?:número|nº|n°)?\s*([0-9]{2,5}[A-Za-z]?)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UnidadePattern_Novo();
    [GeneratedRegex(@"(?<![0-9-])([0-9]{3,4})(?![0-9-]|\s+[0-9]{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UnidadeIsoladaPattern();
    [GeneratedRegex(@"meu nome [eé]\s+([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeMeuNomePattern();
    [GeneratedRegex(@"(?:sou (?:o|a)|aqui é (?:o|a)|me chamo)\s+([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSouPattern();
    [GeneratedRegex(@"(?:Oi|Olá|Boa (?:tarde|noite|dia)|Tudo (?:bem|certo)|E aí|Opa|Fala),?\s*(?:aqui é|sou|me chamo|é)\s*(?:o|a)?\s*([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSaudacaoPattern();
    [GeneratedRegex(@"(?:sou|é)\s+(?:o|a)\s+([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSouOApattern();
    [GeneratedRegex(@"(?:sou|é)\s+([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSouSimplesPattern();
    [GeneratedRegex(@"^([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)\s+(?:quero|vim|vou|preciso|queria|gostaria)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeInicioQueroPattern();
    [GeneratedRegex(@"^([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSimplesPattern();
    [GeneratedRegex(@"^(?:é\s+)?([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)\s*\.?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeSimplesComEPattern();
    [GeneratedRegex(@"^(?:sr\.?|sra\.?|dr\.?|dra\.?|prof\.?)?\s*([A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+(?:\s+[A-ZÁÉÍÓÚÀÂÊÔÇÃÕ][a-záéíóúàâêôçãõ]+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeComTituloPattern();

    [GeneratedRegex(@"\b(cpf|documento|identifica(?:cao|ção)|rg|cnh)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DocumentCuePattern();

    [GeneratedRegex(@"\b(zero|um|uma|dois|duas|tres|três|quatro|cinco|seis|sete|oito|nove)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SpokenDigitPattern();

    [GeneratedRegex(@"\b(?:apto?\.?|apartamento|unidade|sala|bloco\s+\w+\s+apto?\.?)\s*[n°#]?\s*(\d{1,5}[A-Za-z]?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UnidadePattern();

    [GeneratedRegex(@"\bbloco\s*[n°#]?\s*([A-Za-z0-9]{1,6})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BlocoPattern();

    [GeneratedRegex(@"\btorre\s*[n°#]?\s*([A-Za-z0-9]{1,10})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TorrePattern();

    [GeneratedRegex(@"\b(?:Boa\s+(?:tarde|noite|manh[aã])|Ol[aá]|Oi|Bom\s+dia|Salve)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SaudacaoPattern();

    [GeneratedRegex(@"\b(?:tchau|at[eé]\s+logo|obrigad[oa]|encerrando|adeus|at[eé]\s+mais)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DespedidaPattern();

    [GeneratedRegex(@"\b(?:emerg[eê]ncia|socorro|ajuda|urgente|inc[eê]ndio|acidente|ambul[aâ]ncia|pol[ií]cia|bombeiro)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UrgenciaPattern();

    // Captura nomes apos frase introdutoria e para antes de verbos comuns da frase.
    // Ex.: "meu nome e Janlon, quero visitar..." -> captura apenas "Janlon".
    [GeneratedRegex(@"\b(?:me\s+chamo|meu\s+nome\s+[eé]|sou\s+o|sou\s+a|eu\s+sou)\s+([A-ZÁÀÂÃÉÊÍÓÔÕÚÇ][a-záàâãéêíóôõúç]+(?:\s+[A-ZÁÀÂÃÉÊÍÓÔÕÚÇ][a-záàâãéêíóôõúç]+)*)(?=\s*[,\.!\?;:]|\s+(?:quero|preciso|vou|vim|venho|para|pra|no|na|ao|a|o)\b|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeExplicitoPattern();

    // Captura o nome de quem o visitante quer ver: "visitar a Giovanna", "falar com Pedro"
    [GeneratedRegex(@"\b(?:visitar\s+(?:a\s+|o\s+)?|falar\s+com\s+|ver\s+(?:a\s+|o\s+))([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NomeVisitadoPattern();

    // SEM IgnoreCase: sufixos juridicos (S.A., LTDA, ME, EPP) sao uppercase em documentos BR.
    // Com IgnoreCase, palavras terminadas em 'me' (ex: "nome") geravam falso positivo.
    [GeneratedRegex(@"\b(?:da\s+empresa|trabalho\s+na|venho\s+(?:da|pela)|representante\s+da)\s+([A-Z][\w\s\.&]{2,40}(?:S\.A\.|S/A|LTDA\.?|EIRELI\.?|EPP\.?|ME\b))",
        RegexOptions.Compiled)]
    private static partial Regex EmpresaPattern();

    [GeneratedRegex(@"\b(?:sou\s+(?:o|a)\s+)?(?:pai|mae|mãe|filho|filha|irmao|irmão|irma|irmã|esposo|esposa|marido|namorado|namorada|tio|tia|primo|prima|av[ôo]|avo|sogro|sogra)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ParentescoPattern();

    [GeneratedRegex(@"\b(?:estou\s+com\s+(?:carro|ve[ií]culo|moto)|vim\s+de\s+carro|de\s+carro|de\s+moto|com\s+ve[ií]culo)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex VeiculoPattern();

    [GeneratedRegex(@"\b([A-Z]{3}[0-9][A-Z0-9][0-9]{2})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PlacaMercosulPattern();

    [GeneratedRegex(@"\b(?:entregador|entrega|ifood|rappi|mercado\s+livre|correios|motoboy)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EntregadorPattern();

    public RegexEngine(IOptions<AIServiceOptions> options, ILogger<RegexEngine> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<ProcessingLayerResult> ProcessAsync(string texto, CancellationToken ct = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_options.Value.Regex.TimeoutMs);
        try
        {
            var result = ExecutePatterns(texto, timeout);
            _logger.LogDebug("Regex layer: confianca={Conf:P0}, intencao={Int}", result.Confianca, result.Intencao);
            return Task.FromResult(result);
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout na camada Regex para o texto: '{Texto}'", texto[..Math.Min(50, texto.Length)]);
            return Task.FromResult(EmptyResult());
        }
    }

    private ProcessingLayerResult ExecutePatterns(string texto, TimeSpan timeout)
    {
        string? nome = null;
        string? nomeVisitante = null;
        string? nomeVisitado = null;
        string? documento = null;
        string? cpf = null;
        string? unidade = null;
        string? bloco = null;
        string? torre = null;
        string? empresa = null;
        string? parentesco = null;
        bool estaComVeiculo = false;
        string? placa = null;
        bool eEntregador = false;
        Intencao? intencao = null;
        int hits = 0;

        // --- Intencao ---
        if (UrgenciaPattern().IsMatch(texto)) { intencao = Intencao.Urgencia; hits += 3; }
        else if (DespedidaPattern().IsMatch(texto)) { intencao = Intencao.Despedida; hits++; }
        else if (SaudacaoPattern().IsMatch(texto)) { intencao = Intencao.Saudacao; hits++; }


        // --- Documento (CPF) ---
        var cpfMatch = CpfPattern().Match(texto);
        if (cpfMatch.Success)
        {
            documento = cpfMatch.Value;
            cpf = cpfMatch.Value;
            intencao ??= Intencao.Identificacao;
            hits += 2;
        }
        else
        {
            var cpfOnlyDigits = CpfOnlyDigitsPattern().Match(texto);
            if (cpfOnlyDigits.Success)
            {
                documento = cpfOnlyDigits.Groups[1].Value;
                cpf = cpfOnlyDigits.Groups[1].Value;
                intencao ??= Intencao.Identificacao;
                hits += 2;
            }
            else
            {
                var cpfSpaced = CpfSpacedPattern().Match(texto);
                if (cpfSpaced.Success)
                {
                    documento = cpfSpaced.Groups[1].Value.Replace(" ", "");
                    cpf = documento;
                    intencao ??= Intencao.Identificacao;
                    hits += 2;
                }
                else if (CpfSpokenPattern().Match(texto).Success)
                {
                    // fallback: já tratado em TryExtractCpfFromSpokenOrSpacedDigits
                    if (TryExtractCpfFromSpokenOrSpacedDigits(texto, out var normalizedCpf2))
                    {
                        documento = normalizedCpf2;
                        cpf = normalizedCpf2;
                        intencao ??= Intencao.Identificacao;
                        hits += 2;
                    }
                }
            }
        }


        // --- Unidade (Apartamento) ---
        var unidadeMatch = UnidadePattern_Novo().Match(texto);
        if (unidadeMatch.Success)
        {
            unidade = unidadeMatch.Groups[1].Value;
            intencao ??= Intencao.Identificacao;
            hits++;
        }
        else
        {
            var unidadeIsolada = UnidadeIsoladaPattern().Match(texto);
            if (unidadeIsolada.Success)
            {
                unidade = unidadeIsolada.Groups[1].Value;
                intencao ??= Intencao.Identificacao;
                hits++;
            }
        }

        // --- Bloco/Torre ---
        var blocoMatch = BlocoPattern().Match(texto);
        if (blocoMatch.Success)
        {
            bloco = blocoMatch.Groups[1].Value;
            intencao ??= Intencao.Identificacao;
            hits++;
        }

        var torreMatch = TorrePattern().Match(texto);
        if (torreMatch.Success)
        {
            torre = torreMatch.Groups[1].Value;
            intencao ??= Intencao.Identificacao;
            hits++;
        }


        // --- Nome explicito e variações ---
        var nomeMatch = NomeMeuNomePattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSouPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSaudacaoPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSouOApattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSouSimplesPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeInicioQueroPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSimplesPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeSimplesComEPattern().Match(texto);
        if (!nomeMatch.Success) nomeMatch = NomeComTituloPattern().Match(texto);
        if (nomeMatch.Success)
        {
            nome = nomeMatch.Groups[1].Value.Trim();
            nomeVisitante = nome;
            intencao ??= Intencao.Identificacao;
            hits += 2;
            // Autoidentificacao explicita ("meu nome e ...") e um sinal forte.
            hits += 1;
        }

        // --- Nome visitado ("quero visitar a Giovanna") ---
        {
            var nomeVisitadoMatch = NomeVisitadoPattern().Match(texto);
            if (nomeVisitadoMatch.Success)
            {
                nomeVisitado = nomeVisitadoMatch.Groups[1].Value.Trim();
                intencao ??= Intencao.Identificacao;
                hits++;
            }
        }

        // Normaliza nomes: preferimos separar quem fala (visitante) de quem sera visitado.
        // - nomeVisitante: capturado via "meu nome e..."
        // - nome: por padrao representa o alvo da visita quando disponivel
        if (!string.IsNullOrWhiteSpace(nomeVisitado))
            nome = nomeVisitado;
        else if (!string.IsNullOrWhiteSpace(nomeVisitante))
            nome = nomeVisitante;

        if (nomeVisitante is null && nome is not null)
        {
            // fallback: quando nao identificamos explicitamente o visitante,
            // mantemos compatibilidade usando o nome geral.
            nomeVisitante = nome;
        }

        // --- Empresa ---
        var empresaMatch = EmpresaPattern().Match(texto);
        if (empresaMatch.Success)
        {
            empresa = empresaMatch.Groups[1].Value.Trim();
            intencao ??= Intencao.Identificacao;
            hits++;
        }

        // --- Parentesco ---
        var parentescoMatch = ParentescoPattern().Match(texto);
        if (parentescoMatch.Success)
        {
            parentesco = parentescoMatch.Value.Trim();
            intencao ??= Intencao.Identificacao;
            hits++;
        }

        // --- Veículo / Placa ---
        if (VeiculoPattern().IsMatch(texto))
        {
            estaComVeiculo = true;
            hits++;
        }

        var placaMatch = PlacaMercosulPattern().Match(texto);
        if (placaMatch.Success)
        {
            placa = placaMatch.Groups[1].Value.ToUpperInvariant();
            estaComVeiculo = true;
            hits += 2;
        }

        // --- Entregador ---
        if (EntregadorPattern().IsMatch(texto))
        {
            eEntregador = true;
            intencao ??= Intencao.Identificacao;
            hits += 2;
        }

        // Bonus de completude: nome + unidade/documento = identificacao suficientemente completa
        if ((nome is not null || nomeVisitante is not null) && (unidade is not null || documento is not null || cpf is not null))
            hits += 3;

        double confianca = hits == 0 ? 0.0 : Math.Min(0.97, 0.30 + (hits * 0.13));

        return new ProcessingLayerResult
        {
            Camada = "Regex",
            Confianca = confianca,
            Intencao = intencao,
            DadosExtraidos = new()
            {
                Nome = nome,
                NomeVisitante = nomeVisitante ?? nome,
                Documento = documento,
                Cpf = cpf,
                Unidade = unidade,
                Bloco = bloco,
                Torre = torre,
                Empresa = empresa,
                Parentesco = parentesco,
                EstaComVeiculo = estaComVeiculo,
                Placa = placa,
                EEntregador = eEntregador
            }
        };
    }

    private static ProcessingLayerResult EmptyResult() => new()
    {
        Camada = "Regex",
        Confianca = 0.0,
        Intencao = null,
        DadosExtraidos = new()
    };

    private static bool TryExtractCpfFromSpokenOrSpacedDigits(string texto, out string cpf)
    {
        cpf = string.Empty;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        var normalized = SpokenDigitPattern().Replace(texto, static match =>
        {
            return match.Value.ToLowerInvariant() switch
            {
                "zero" => "0",
                "um" or "uma" => "1",
                "dois" or "duas" => "2",
                "tres" or "três" => "3",
                "quatro" => "4",
                "cinco" => "5",
                "seis" => "6",
                "sete" => "7",
                "oito" => "8",
                "nove" => "9",
                _ => string.Empty
            };
        });

        var digitsOnly = new string(normalized.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length != 11)
            return false;

        var numericTokenCount = normalized
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', ';', ':', '-', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Count(token => token.All(char.IsDigit));

        var hasDocumentCue = DocumentCuePattern().IsMatch(texto);
        if (!hasDocumentCue && numericTokenCount < 9)
            return false;

        cpf = digitsOnly;
        return true;
    }
}
