namespace SMSMarica.Data.Entities;

/// <summary>
/// Especialidade médica — tabela de referência local do agendamento (ex.: "Cardiologia",
/// "Oftalmologia"). O médico vem do FHIR (Practitioner) e tem uma especialidade textual lá;
/// aqui mantemos a lista controlada da SMS para montar agendas por unidade. Ver ADR-0012.
/// </summary>
public class Especialidade
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Código CBO (Classificação Brasileira de Ocupações) opcional, alinha com o FHIR/SUS.</summary>
    public string? CodigoCbo { get; set; }

    /// <summary>Visibilidade no agendamento. Soft-delete via <see cref="ExcluidoEm"/>.</summary>
    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
