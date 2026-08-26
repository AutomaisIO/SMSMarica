using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Fila durável (outbox) do robô de atendimento. O handler do webhook, ao decidir que o robô
/// deve responder, apenas ENFILEIRA uma tarefa (commit atômico com o inbound) — a chamada à IA
/// roda depois, fora do caminho do webhook, no <c>RoboAtendimentoWorker</c>. Uma tarefa por
/// mensagem inbound (<see cref="MensagemWhatsAppId"/> único) garante idempotência.
/// </summary>
public class RoboAtendimentoTarefa
{
    public Guid Id { get; set; }

    public Guid ConversaId { get; set; }

    /// <summary>Mensagem inbound que originou a tarefa — chave de idempotência do enfileiramento.</summary>
    public Guid MensagemWhatsAppId { get; set; }

    public Guid? PacienteId { get; set; }

    /// <summary>Assunto resolvido pela classificação (preenchido no processamento).</summary>
    public Guid? RoboAssuntoId { get; set; }

    public StatusRoboTarefa Status { get; set; } = StatusRoboTarefa.Pendente;

    /// <summary>Sessão do motor de IA reusada pelos turnos da mesma janela.</summary>
    public string? SessionIdAiengine { get; set; }

    public double? ConfiancaUltima { get; set; }

    // Consumo do turno de IA (do ResultMessage do aiengine) — para o relatório de custo do robô.
    /// <summary>Tokens de entrada do turno (input + cache lido/criado). <c>null</c> = não medido.</summary>
    public long? TokensEntrada { get; set; }
    /// <summary>Tokens de saída (geração) do turno. <c>null</c> = não medido.</summary>
    public long? TokensSaida { get; set; }
    /// <summary>Custo do turno em USD (total_cost_usd do aiengine, já considera cache).</summary>
    public decimal? CustoUsd { get; set; }

    public int Tentativas { get; set; }

    public DateTime? ProximaTentativaEm { get; set; }

    public string? Erro { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Conversa? Conversa { get; set; }
    public RoboAssunto? RoboAssunto { get; set; }
    public MensagemWhatsApp? Mensagem { get; set; }
}
