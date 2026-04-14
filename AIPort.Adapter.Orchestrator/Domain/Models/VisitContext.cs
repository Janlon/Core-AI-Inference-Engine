namespace AIPort.Adapter.Orchestrator.Domain.Models;

/// <summary>
/// Objeto de estado que controla o preenchimento incremental de slots de visita residencial.
/// Cada propriedade representa um dado obrigatório para liberação do visitante.
/// Inclui suporte para anti-loop detection e confirmação implícita.
/// </summary>
public sealed class VisitContext
{
    /// <summary>
    /// Se false, bloco nunca será obrigatório (será pulado no loop de slot-filling).
    /// </summary>
    public bool RequiresBlock { get; set; }

    /// <summary>
    /// Se false, torre nunca será obrigatória (será pulada no loop de slot-filling).
    /// </summary>
    public bool RequiresTower { get; set; }

    public string? VisitorName  { get; set; }
    public string? ResidentName { get; set; }
    public string? Apartment    { get; set; }
    public string? Block        { get; set; }
    public string? Tower        { get; set; }
    public string? Document     { get; set; }
    public string? Parentesco   { get; set; }

    /// <summary>
    /// Rastreia as últimas 3 respostas para detectar loops e repetições.
    /// Usado para implementar anti-loop e confirmação implícita.
    /// </summary>
    private readonly Queue<(string Slot, string? Value)> _lastExtractions = new(3);

    /// <summary>
    /// Contador de rodadas com o mesmo slot sem mudança. Se ≥ 2, trigger confirmação.
    /// </summary>
    private int _consecutiveUnchangedRounds = 0;

    /// <summary>
    /// Retorna <c>true</c> quando todos os slots obrigatórios estão preenchidos e o
    /// fluxo de perguntas pode ser interrompido para ir direto à notificação do morador.
    /// Respeita <see cref="RequiresBlock"/> e <see cref="RequiresTower"/> para determinar
    /// quais slots são realmente obrigatórios.
    /// </summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(VisitorName)  &&
        !string.IsNullOrWhiteSpace(ResidentName) &&
        !string.IsNullOrWhiteSpace(Apartment)    &&
        (!RequiresBlock || !string.IsNullOrWhiteSpace(Block))   &&
        (!RequiresTower || !string.IsNullOrWhiteSpace(Tower))   &&
        !string.IsNullOrWhiteSpace(Document);

    /// <summary>
    /// Retorna <c>true</c> se o sistema detectou repetição excessiva no mesmo slot
    /// (ex: usuário repetiu "204 204" 2+ vezes sem mudar).
    /// </summary>
    public bool IsLoopDetected => _consecutiveUnchangedRounds >= 2;

    /// <summary>
    /// Retorna <c>true</c> se houve mudança no último merge.
    /// Usado para detectar se uma extração trouxe novos dados.
    /// </summary>
    public bool HasChangedSinceLastMerge { get; private set; } = false;

    /// <summary>
    /// Preenche os slots ainda nulos a partir dos dados extraídos pela IA.
    /// Slots já preenchidos nunca são sobrescritos.
    /// </summary>
    public void MergeFrom(DadosExtraidosDto dados)
    {
        HasChangedSinceLastMerge = false;

        var incomingVisitorName = NormalizeSlotValue(dados.NomeVisitante);
        var incomingResidentCandidate = NormalizeSlotValue(dados.Nome);

        // Visitante: campo explícito tem prioridade sobre o genérico "Nome"
        if (string.IsNullOrWhiteSpace(VisitorName) && incomingVisitorName is not null)
        {
            VisitorName = incomingVisitorName;
            HasChangedSinceLastMerge = true;
        }

        // "Nome" é mapeado para o morador; se o visitante ainda não foi identificado
        // e o morador também não, o primeiro turno preenche o visitante.
        if (incomingResidentCandidate is not null)
        {
            if (string.IsNullOrWhiteSpace(VisitorName) && string.IsNullOrWhiteSpace(ResidentName))
            {
                VisitorName = incomingResidentCandidate;
                HasChangedSinceLastMerge = true;
            }
            else if (
                string.IsNullOrWhiteSpace(ResidentName)
                && !string.Equals(incomingResidentCandidate, VisitorName, StringComparison.OrdinalIgnoreCase))
            {
                ResidentName = incomingResidentCandidate;
                HasChangedSinceLastMerge = true;
            }
        }

        if (string.IsNullOrWhiteSpace(Apartment) && !string.IsNullOrWhiteSpace(dados.Unidade))
        {
            Apartment = dados.Unidade;
            HasChangedSinceLastMerge = true;
        }

        if (string.IsNullOrWhiteSpace(Block) && !string.IsNullOrWhiteSpace(dados.Bloco))
        {
            Block = dados.Bloco;
            HasChangedSinceLastMerge = true;
        }

        if (string.IsNullOrWhiteSpace(Tower) && !string.IsNullOrWhiteSpace(dados.Torre))
        {
            Tower = dados.Torre;
            HasChangedSinceLastMerge = true;
        }

        if (string.IsNullOrWhiteSpace(Document))
        {
            if (!string.IsNullOrWhiteSpace(dados.Documento))
            {
                Document = dados.Documento;
                HasChangedSinceLastMerge = true;
            }
            else if (!string.IsNullOrWhiteSpace(dados.Cpf))
            {
                Document = dados.Cpf;
                HasChangedSinceLastMerge = true;
            }
        }

        if (string.IsNullOrWhiteSpace(Parentesco) && !string.IsNullOrWhiteSpace(dados.Parentesco))
        {
            Parentesco = dados.Parentesco;
            HasChangedSinceLastMerge = true;
        }
    }

    private static string? NormalizeSlotValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /// <summary>
    /// Rastreia a última extração e atualiza o contador de rodadas sem mudança.
    /// Retorna <c>true</c> se não houve mudança no valor do slot (loop detectado).
    /// </summary>
    public bool TrackExtractionAttempt(string slotName, string? extractedValue)
    {
        var key = (slotName, extractedValue);
        _lastExtractions.Enqueue(key);
        if (_lastExtractions.Count > 3)
            _lastExtractions.Dequeue();

        // Se as últimas 2 tentativas no mesmo slot tiveram o mesmo valor, incrementa contador
        var recent = _lastExtractions.Where(x => x.Slot == slotName).ToList();
        if (recent.Count >= 2)
        {
            var lastTwo = recent.TakeLast(2).ToList();
            if (lastTwo[0].Value == lastTwo[1].Value)
            {
                _consecutiveUnchangedRounds++;
                return true; // Loop detectado
            }
        }

        _consecutiveUnchangedRounds = 0;
        return false;
    }

    /// <summary>
    /// Retorna uma pergunta implícita que confirma o que foi extraído,
    /// em vez de repetir a pergunta original. Exemplo:
    /// "Entendido, você está no bloco 12. Qual o número do apartamento?"
    /// </summary>
    public string? GetConfirmationPrompt()
    {
        if (!string.IsNullOrWhiteSpace(Block) && string.IsNullOrWhiteSpace(Apartment))
            return $"Entendido, você está no bloco {Block}. Qual o número do apartamento?";

        if (!string.IsNullOrWhiteSpace(Tower) && string.IsNullOrWhiteSpace(Block))
            return $"Entendido, você está na torre {Tower}. Qual o bloco?";

        if (!string.IsNullOrWhiteSpace(Apartment) && string.IsNullOrWhiteSpace(ResidentName))
            return $"Apartamento {Apartment} anotado. Qual o nome do morador que deseja visitar?";

        return null;
    }

    /// <summary>
    /// Retorna o texto da próxima pergunta que deve ser feita ao visitante,
    /// solicitando apenas o primeiro slot ainda nulo que seja obrigatório.
    /// Respeita <see cref="RequiresBlock"/> e <see cref="RequiresTower"/>.
    /// Retorna <c>null</c> quando todos os slots obrigatórios estão preenchidos
    /// (<see cref="IsComplete"/> é <c>true</c>).
    /// </summary>
    public string? GetNextPrompt()
    {
        if (string.IsNullOrWhiteSpace(VisitorName))
            return "Por favor, informe seu nome completo.";

        if (string.IsNullOrWhiteSpace(ResidentName))
            return "Informe o nome do morador que deseja visitar.";

        if (string.IsNullOrWhiteSpace(Apartment))
            return "Informe o número do apartamento.";

        if (RequiresBlock && string.IsNullOrWhiteSpace(Block))
            return "Informe o bloco.";

        if (RequiresTower && string.IsNullOrWhiteSpace(Tower))
            return "Informe a torre.";

        if (string.IsNullOrWhiteSpace(Document))
            return "Por fim, informe seu documento de identificação.";

        return null;
    }
}
