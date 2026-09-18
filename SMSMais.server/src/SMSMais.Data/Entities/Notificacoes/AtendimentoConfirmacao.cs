using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>
/// "Estou atendendo esta solicitação": posse humana de uma confirmação de agendamento no menu
/// Confirmações. Substitui a planilha em que as atendentes marcavam quem ligou para quem.
///
/// <para>No máximo UMA linha ativa por solicitação (índice único filtrado por
/// <c>encerrado_em IS NULL</c>). A posse é trava de ação: só quem atende (ou quem assume
/// explicitamente) confirma, cancela ou estaciona. Corrida no "Assumir" é resolvida pelo
/// <see cref="RowVersion"/> (xmin) — mesmo padrão da posse de conversa (ADR-0047).</para>
///
/// <para>Não duplica o estado da solicitação: confirmado/cancelado continuam em
/// <see cref="Solicitacao.StatusConfirmacao"/> e <see cref="Solicitacao.Status"/>; aqui fica só
/// quem fez, quando e por quê.</para>
/// </summary>
public class AtendimentoConfirmacao
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    /// <summary>Atendente que está com a solicitação (ou que a estacionou).</summary>
    public Guid AtendenteUsuarioId { get; set; }
    public Usuario? Atendente { get; set; }

    public SituacaoAtendimentoConfirmacao Situacao { get; set; } = SituacaoAtendimentoConfirmacao.EmAtendimento;

    /// <summary>Motivo da pendência / do cancelamento / observação do contato errado.</summary>
    public string? Motivo { get; set; }

    public DateTime IniciadoEm { get; set; }

    /// <summary>Null enquanto a solicitação está com alguém ou estacionada.</summary>
    public DateTime? EncerradoEm { get; set; }

    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin) — corrida de posse.</summary>
    public uint RowVersion { get; set; }

    public ICollection<AtendimentoConfirmacaoEvento> Eventos { get; set; } = [];
}
