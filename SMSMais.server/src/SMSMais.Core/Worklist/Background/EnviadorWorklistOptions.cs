namespace SMSMais.Core.Worklist.Background;

public sealed class EnviadorWorklistOptions
{
    public const string SecaoConfig = "EnviadorWorklist";

    /// <summary>Intervalo entre passagens do worker. Default 15s.</summary>
    public int IntervaloSegundos { get; set; } = 15;

    /// <summary>
    /// Máximo de solicitações processadas por passagem (limita carga em caso
    /// de fila grande pós-outage do PACS).
    /// </summary>
    public int MaximoPorPassagem { get; set; } = 25;
}
