namespace SMSMarica.Data.Entities;

/// <summary>
/// Uma versão do SQL de um <see cref="Indicador"/>. Toda gravação do motor grava aqui antes
/// de sobrescrever — o aprimoramento do indicador fica rastreável e reversível.
/// </summary>
public class IndicadorVersao
{
    public Guid Id { get; set; }

    public Guid IndicadorId { get; set; }
    public Indicador? Indicador { get; set; }

    /// <summary>Sequencial por indicador (1, 2, 3…), para exibir "v3".</summary>
    public int Numero { get; set; }

    public string Sql { get; set; } = string.Empty;

    /// <summary>O que mudou nesta versão (preenchido por quem editou).</summary>
    public string? Nota { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
