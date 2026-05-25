namespace SMSMarica.Data.Entities;

/// <summary>
/// Catálogo oficial SIGTAP (DataSUS) — espelho do CSV mensal publicado em
/// sigtap.datasus.gov.br. Read-only para o operador comum; atualizado por job
/// administrativo. Sem audit fields (origem externa).
/// </summary>
public class ProcedimentoSigtap
{
    public Guid Id { get; set; }

    /// <summary>Código formatado, ex.: "02.04.03.018-8".</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    public string Grupo { get; set; } = string.Empty;

    public string Subgrupo { get; set; } = string.Empty;

    public string Forma { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public DateOnly CompetenciaInicio { get; set; }

    public DateOnly? CompetenciaFim { get; set; }
}
