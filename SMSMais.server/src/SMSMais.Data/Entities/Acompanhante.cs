using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Pessoa que pode acompanhar o paciente no Transporte de Pacientes. A lista é do PACIENTE e vale
/// para todos os atendimentos dele; em cada viagem se escolhe quem vai
/// (<see cref="SessaoAcompanhante"/>), até o limite do atendimento.
///
/// <para>Vive no smsmarica, não no hub FHIR: o domínio de transporte é operacional (a régua do
/// projeto). Se a pessoa também é paciente, <see cref="PacienteVinculadoId"/> aponta para ela —
/// mas nunca se cria paciente só por ser acompanhante.</para>
/// </summary>
public class Acompanhante
{
    public Guid Id { get; set; }

    /// <summary>Paciente acompanhado (id do Patient no hub FHIR; sem FK).</summary>
    public Guid PacienteId { get; set; }

    /// <summary>CPF só com dígitos, dígito verificador conferido.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Nome conferido pelo par CPF + nascimento (nunca digitado).</summary>
    public string Nome { get; set; } = string.Empty;

    public DateOnly DataNascimento { get; set; }
    public ParentescoAcompanhante? Parentesco { get; set; }
    public string? Telefone { get; set; }

    /// <summary>Se o acompanhante também é paciente na base, o id dele no hub FHIR.</summary>
    public Guid? PacienteVinculadoId { get; set; }

    public FonteNomeAcompanhante FonteNome { get; set; }
    public OrigemCadastroAcompanhante Origem { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Usuário do painel que cadastrou; nulo quando foi o próprio paciente pelo app.</summary>
    public Guid? CriadoPor { get; set; }

    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
