using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

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

    // ---- Leitura/notificação (ticket #42) ----

    /// <summary>
    /// Última vez que a gestão respondeu ao autor (conclusão/negação ou comentário público).
    /// Base da "bandeira" do autor: há resposta a reconhecer quando este valor é mais recente
    /// que <see cref="RespostaReconhecidaEm"/>.
    /// </summary>
    public DateTime? RespondidoEm { get; set; }

    /// <summary>Quando o autor reconheceu a última resposta (clicou em "reconhecer" ou abriu o ticket).</summary>
    public DateTime? RespostaReconhecidaEm { get; set; }

    /// <summary>
    /// Última vez que a gestão visualizou/atuou no ticket (inbox compartilhado). Um ticket é "novo"
    /// para a gestão enquanto isto é nulo ou anterior à última atividade (<see cref="AtualizadoEm"/>/<see cref="CriadoEm"/>).
    /// </summary>
    public DateTime? VistoPelaGestaoEm { get; set; }

    /// <summary>
    /// Quando a gestão encaminhou o ticket ao Agente IA (botão "Enviar ao Agente IA").
    /// Não-nulo = já foi enviado; base da marca "Enviado à IA" na lista da gestão.
    /// </summary>
    public DateTime? EnviadoIaEm { get; set; }

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
