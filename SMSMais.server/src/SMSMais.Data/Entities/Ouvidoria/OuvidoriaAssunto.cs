namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Assunto da manifestação em dois níveis (assunto → subassunto) via <see cref="PaiId"/>.
/// Os 22 assuntos do Manual do MS 2014 são semeados pelo service na primeira leitura
/// (nunca em migration). <see cref="CodigoOuvidorSus"/> fica reservado para a exportação futura.
/// </summary>
public sealed class OuvidoriaAssunto
{
    public Guid Id { get; set; }

    /// <summary>Assunto pai; nulo = 1º nível.</summary>
    public Guid? PaiId { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Código equivalente no OuvidorSUS, quando houver (fase 3).</summary>
    public string? CodigoOuvidorSus { get; set; }

    /// <summary>Ordem de exibição dentro do mesmo pai.</summary>
    public int Ordem { get; set; }

    public bool Ativo { get; set; } = true;

    public OuvidoriaAssunto? Pai { get; set; }
}
