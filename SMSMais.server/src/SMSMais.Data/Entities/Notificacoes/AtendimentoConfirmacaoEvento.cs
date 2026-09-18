using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// Trilha append-only do atendimento humano de confirmação: quem atendeu, assumiu, transferiu,
/// confirmou, cancelou, estacionou. De/Para são snapshots (Guid solto) — auditoria que não quebra
/// se um usuário for removido.
/// </summary>
public class AtendimentoConfirmacaoEvento
{
    public Guid Id { get; set; }
    public Guid AtendimentoId { get; set; }
    public AtendimentoConfirmacao? Atendimento { get; set; }

    public TipoEventoAtendimentoConfirmacao Tipo { get; set; }

    /// <summary>Quem executou a ação. <c>null</c> = sistema.</summary>
    public Guid? AtorUsuarioId { get; set; }
    public Guid? DeUsuarioId { get; set; }
    public Guid? ParaUsuarioId { get; set; }

    public string? Observacao { get; set; }

    public DateTime OcorridoEm { get; set; }
}
