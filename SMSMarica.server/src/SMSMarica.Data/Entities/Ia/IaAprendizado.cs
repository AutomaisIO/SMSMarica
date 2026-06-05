using Pgvector;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Aprendizado incremental por fonte (hint, exemplo, glossário, correção). Recuperável por
/// similaridade junto com o conhecimento base. Correções nascem com <see cref="Origem"/> = Auto
/// e <see cref="Ativo"/> = true; podem ser desativadas/removidas pela governança.
/// </summary>
public class IaAprendizado
{
    public Guid Id { get; set; }
    public Guid FonteId { get; set; }
    public IaFonte? Fonte { get; set; }

    public TipoAprendizado Tipo { get; set; }
    public OrigemAprendizado Origem { get; set; }
    public string Conteudo { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
