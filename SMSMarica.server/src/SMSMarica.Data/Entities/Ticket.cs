using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Ticket de suporte: bug, mudança, sugestão ou dúvida aberto por um usuário do sistema.
/// Autor = <see cref="CriadoPor"/>. O fluxo de status é <c>Aberto → EmAnalise → Concluido | Negado</c>.
/// </summary>
public sealed class Ticket
{
    public Guid Id { get; set; }

    /// <summary>
    /// Número sequencial curto (identity do Postgres) — referência humana do ticket ("#42")
    /// para o usuário citar por telefone/mensagem. O Guid continua sendo a chave técnica.
    /// </summary>
    public int Numero { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;

    public TicketTipo Tipo { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Aberto;
    public TicketPrioridade Prioridade { get; set; } = TicketPrioridade.Normal;

    /// <summary>Retorno da equipe ao fechar o ticket: feedback (Concluido) ou justificativa (Negado).</summary>
    public string? RespostaFinal { get; set; }

    /// <summary>Unidade ativa do autor no momento da abertura (usada no modo de visibilidade PorUnidade).</summary>
    public Guid? UnidadeId { get; set; }

    /// <summary>Arquivado pelo próprio autor (some da lista dele, sem excluir).</summary>
    public DateTime? ArquivadoPeloAutorEm { get; set; }

    /// <summary>Arquivado pela equipe/admin (some da gestão, sem excluir).</summary>
    public DateTime? ArquivadoPeloAdminEm { get; set; }

    // ---- Auditoria ADR-0006 (CriadoPor = autor do ticket) ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }

    public ICollection<TicketComentario> Comentarios { get; set; } = [];
    public ICollection<TicketAnexo> Anexos { get; set; } = [];
}
