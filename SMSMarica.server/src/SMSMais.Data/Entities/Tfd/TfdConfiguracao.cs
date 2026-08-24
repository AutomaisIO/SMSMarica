namespace SMSMais.Data.Entities.Tfd;

/// <summary>
/// Configuração (linha única) do TFD: valor por unidade de faturamento (a cada
/// <see cref="KmPorUnidade"/> km) e código SIGTAP do procedimento de transporte.
/// </summary>
/// <remarks>
/// Antiga <c>TfdConfigFaturamento</c> / <c>tfd_config_faturamento</c>. Continua sendo do
/// TFD — só adota o sufixo <c>_configuracao</c> em pt-BR usado por
/// <c>ia_configuracao</c>, <c>laudo_configuracao</c>, <c>sisreg_configuracao</c> e
/// <c>ticket_configuracao</c> (ADR-0038).
/// </remarks>
public class TfdConfiguracao
{
    public Guid Id { get; set; }

    /// <summary>Valor (R$) por unidade — cada <see cref="KmPorUnidade"/> km rodados.</summary>
    public decimal ValorPor50Km { get; set; }

    /// <summary>Km que compõem 1 unidade de faturamento (padrão 50).</summary>
    public int KmPorUnidade { get; set; } = 50;

    /// <summary>Código SIGTAP do procedimento de transporte (a configurar).</summary>
    public string? CodigoSigtap { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
