using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Fila de notificação WhatsApp de um agendamento (exame hoje; consulta no futuro).
/// Criada no import (SISREG) quando a DataAgendada é futura; um worker
/// (NotificadorAgendamentoService) processa Pendentes com ProximaTentativaEm vencida,
/// gera o magic link e envia o template com botões. Os recibos de entrega/leitura
/// (webhook value.statuses) são espelhados aqui para a tela de gestão.
/// </summary>
public class AgendamentoNotificacao
{
    public Guid Id { get; set; }

    public TipoAgendamento Tipo { get; set; } = TipoAgendamento.Exame;

    /// <summary>Preenchida quando Tipo=Exame (única notificação por solicitação).</summary>
    public Guid? SolicitacaoExameId { get; set; }
    public SolicitacaoExame? SolicitacaoExame { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    /// <summary>Telefone canônico (55DDD9XXXXXXXX) escolhido na hora do envio. Null antes do 1º envio.</summary>
    public string? Telefone { get; set; }

    public StatusNotificacaoAgendamento Status { get; set; } = StatusNotificacaoAgendamento.Pendente;

    /// <summary>Motivo legível da falha (erro Meta code/título) ou "sem celular válido".</summary>
    public string? MotivoFalha { get; set; }

    /// <summary>Magic link ativo desta notificação (renovado a cada reenvio).</summary>
    public Guid? LoginLinkId { get; set; }
    public CidadaoLoginLink? LoginLink { get; set; }

    /// <summary>Última mensagem template enviada (rastreia recibos por wamid).</summary>
    public Guid? MensagemWhatsAppId { get; set; }
    public MensagemWhatsApp? MensagemWhatsApp { get; set; }

    // ---- Retry (mesmo padrão do EnviadorWorklistService) ----

    public int Tentativas { get; set; }
    public DateTime? UltimaTentativaEm { get; set; }

    /// <summary>Quando o worker deve (re)tentar. Null = terminal.</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    // ---- Recibos ----

    public DateTime? EnviadoEm { get; set; }
    public DateTime? EntregueEm { get; set; }
    public DateTime? LidoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
