using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Consentimento registrado de que o paciente NÃO vai validar o número de WhatsApp, com o
/// motivo. É a válvula de escape do gate de contato verificado na recepção (autorização
/// presencial do exame).
///
/// Append-only: trocar o motivo revoga a linha anterior e cria outra, de modo que o histórico
/// de "quem dispensou o quê e quando" nunca é sobrescrito. No máximo UMA ativa por paciente
/// (<c>revogado_em IS NULL</c>, garantido por índice único filtrado).
///
/// Vive em <c>smsmarica</c>, não no hub FHIR: é ato administrativo do balcão (quem, quando, por
/// quê), não atributo de identidade do cidadão. O verificado continua sendo o marcador no
/// telecom do Patient FHIR — fonte única, sem espelho local.
/// </summary>
public class DispensaVerificacaoContato
{
    public Guid Id { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    public MotivoDispensaContato Motivo { get; set; }

    /// <summary>Texto livre — obrigatório quando <see cref="Motivo"/> é
    /// <see cref="MotivoDispensaContato.Outro"/>, opcional nos demais.</summary>
    public string? MotivoDescricao { get; set; }

    /// <summary>
    /// O operador afirma que o paciente foi informado de que não receberá avisos por WhatsApp e
    /// concordou. Sem isso não há dispensa (o service recusa) — a coluna existe para que a
    /// afirmação fique registrada, não para ser consultada como flag.
    /// </summary>
    public bool PacienteCiente { get; set; }

    /// <summary>Telefone principal que estava no cadastro no momento da dispensa (auditoria).</summary>
    public string? TelefoneNaEpoca { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    /// <summary>Preenchido quando a dispensa deixa de valer. Ativa = null.</summary>
    public DateTime? RevogadoEm { get; set; }
    public Guid? RevogadoPor { get; set; }

    /// <summary>Por que a dispensa caiu ("contato verificado", "telefone alterado", motivo do
    /// operador ao revogar na mão).</summary>
    public string? RevogadoMotivo { get; set; }
}
