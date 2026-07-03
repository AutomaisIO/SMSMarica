using SMSMarica.Data.Entities.Conversas;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Tfd;

/// <summary>
/// Trilha de auditoria das mensagens WhatsApp (Meta Cloud API) — envios e recebimentos, hoje
/// transversal a todo o SMSMarica (não só TFD). A idempotência do webhook se apoia em
/// <see cref="WaMessageId"/>. Colunas <c>conversa_*</c> são aditivas (nullable): linhas legadas
/// (anteriores ao módulo Conversas) permanecem com <see cref="ConversaId"/> nulo.
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

    // Módulo Conversas (aditivo, nullable) --------------------------------------------------

    /// <summary>Conversa (thread) a que a mensagem pertence. Nulo em linhas legadas.</summary>
    public Guid? ConversaId { get; set; }

    /// <summary>Operador que enviou pelo painel. Nulo = inbound do cidadão ou automação.</summary>
    public Guid? AutorUsuarioId { get; set; }

    /// <summary>Snapshot do nome de exibição do operador no momento do envio (histórico correto
    /// após rename/takeover).</summary>
    public string? AutorNomeExibicao { get; set; }

    /// <summary>Natureza da mensagem. Nulo em linhas legadas (tratar como texto/template).</summary>
    public TipoMensagem? TipoMensagem { get; set; }

    public SessaoDeTratamento? Sessao { get; set; }
    public Conversa? Conversa { get; set; }
}
