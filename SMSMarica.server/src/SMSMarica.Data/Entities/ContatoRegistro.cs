using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Registro MANUAL de tentativa de contato com o paciente sobre uma solicitação de exame
/// ("liguei, não atendeu", "caixa postal", "número inválido"...). Complementa as comunicações
/// automáticas (<see cref="ComunicacaoPaciente"/>) na linha do tempo/auditoria da solicitação.
/// Append-only — nunca editado/excluído.
/// </summary>
public class ContatoRegistro
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public Solicitacao? Solicitacao { get; set; }

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK local (espelho da solicitação).</summary>
    public Guid PacienteId { get; set; }

    public MeioContato Meio { get; set; }
    public ResultadoContato Resultado { get; set; }

    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Atendente que registrou o contato.</summary>
    public Guid? CriadoPor { get; set; }
}
