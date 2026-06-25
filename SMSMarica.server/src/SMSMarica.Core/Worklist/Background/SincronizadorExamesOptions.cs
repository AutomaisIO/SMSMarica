namespace SMSMarica.Core.Worklist.Background;

public sealed class SincronizadorExamesOptions
{
    public const string SecaoConfig = "Sincronizador";

    /// <summary>Intervalo entre polls. Default 30s.</summary>
    public int IntervaloSegundos { get; set; } = 30;

    /// <summary>Janela retroativa para considerar solicitações ativas. Default 7 dias.</summary>
    public int JanelaConsultaDias { get; set; } = 7;

    /// <summary>Teto de solicitações processadas por passagem (limita a carga QIDO no PACS). Default 100.</summary>
    public int MaximoPorPassagem { get; set; } = 100;
}
