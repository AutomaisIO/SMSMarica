namespace SMSMais.Core.Worklist.Background;

public sealed class SincronizadorExamesOptions
{
    public const string SecaoConfig = "Sincronizador";

    /// <summary>Intervalo entre polls. Default 30s.</summary>
    public int IntervaloSegundos { get; set; } = 30;

    /// <summary>
    /// Janela retroativa (em dias) pela DATA DO EXAME (StudyDate) para varrer studies no PACS.
    /// Não é a data da solicitação — o exame é sempre recente; a solicitação pode ser antiga.
    /// Default 7 dias.
    /// </summary>
    public int JanelaConsultaDias { get; set; } = 7;

    /// <summary>Teto de studies varridos por passagem (limita a carga QIDO no PACS). Default 500.</summary>
    public int MaximoPorPassagem { get; set; } = 500;

    /// <summary>
    /// Quanto tempo o PACS pode falhar SEM interrupção antes de virar erro (e aviso no celular).
    /// Antes disso a falha fica como Warning: o restart semanal do dcm4chee (~1 min, domingo
    /// 00:00) e soluços de rede não acordam ninguém. Default 120 s.
    /// </summary>
    public int ToleranciaFalhaSegundos { get; set; } = 120;
}
