namespace SMSMarica.Data.Entities.Tfd;

/// <summary>
/// Configuração (linha única) do faturamento TFD: valor por unidade (a cada
/// <see cref="KmPorUnidade"/> km) e código SIGTAP do procedimento de transporte.
/// </summary>
public class TfdConfigFaturamento
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
