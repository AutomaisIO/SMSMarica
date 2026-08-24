namespace SMSMais.Data.Entities;

/// <summary>
/// Tipo (catálogo) de tratamento disponível no município: Hemodiálise,
/// Radioterapia, etc. Populado via seed na migration — não há tela
/// administrativa dedicada por ora.
/// </summary>
public class TipoTratamento
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}
