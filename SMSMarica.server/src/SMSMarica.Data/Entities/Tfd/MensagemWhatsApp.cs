using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Tfd;

/// <summary>
/// Trilha de auditoria das mensagens WhatsApp (Meta Cloud API) do TFD — envios e
/// recebimentos. A idempotência do webhook se apoia em <see cref="WaMessageId"/>.
/// </summary>
public class MensagemWhatsApp
{
    public Guid Id { get; set; }
    public Guid? SessaoId { get; set; }

    /// <summary>Identidade do paciente no hub FHIR (sem FK local).</summary>
    public Guid? PacienteId { get; set; }

    public string Telefone { get; set; } = string.Empty;
    public string? Template { get; set; }
    public DirecaoMensagem Direcao { get; set; }
    public string? Conteudo { get; set; }
    public StatusMensagemWhatsApp Status { get; set; }

    /// <summary>ID da mensagem na Meta (wamid) — único quando presente.</summary>
    public string? WaMessageId { get; set; }

    /// <summary>wamid da mensagem respondida (contexto), quando houver.</summary>
    public string? ContextoWaMessageId { get; set; }

    public DateTime OcorridoEm { get; set; }
    public DateTime CriadoEm { get; set; }

    public SessaoDeTratamento? Sessao { get; set; }
}
