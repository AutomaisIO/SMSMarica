using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Conversas;

/// <summary>
/// Trilha append-only dos eventos de atribuição/encaminhamento de uma conversa: quem pegou,
/// transferiu para quem/qual unidade, e quando. Os campos De/Para são snapshots (Guid solto,
/// sem FK) — auditoria histórica que não deve quebrar se um usuário/unidade for removido.
/// </summary>
public class ConversaEvento
{
    public Guid Id { get; set; }
    public Guid ConversaId { get; set; }
    public TipoEventoConversa Tipo { get; set; }

    /// <summary>Quem executou a ação (operador). <c>null</c> = sistema/automação.</summary>
    public Guid? AtorUsuarioId { get; set; }

    public Guid? DeUsuarioId { get; set; }
    public Guid? ParaUsuarioId { get; set; }
    public Guid? DeUnidadeId { get; set; }
    public Guid? ParaUnidadeId { get; set; }

    public string? Observacao { get; set; }

    public DateTime OcorridoEm { get; set; }
    public DateTime CriadoEm { get; set; }

    public Conversa? Conversa { get; set; }
}
