using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Patient.name[]</c> (HumanName). Pode haver vários: oficial,
/// social, afetivo, apelido, antigo. <c>Given</c> é multi-valorado
/// (nome composto). <c>Family</c> é único.
/// </summary>
public sealed class PatientName
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>FHIR HumanName.use.</summary>
    public NameUse Use { get; set; } = NameUse.Official;

    /// <summary>FHIR HumanName.text. Representação completa do nome (denormalized).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>FHIR HumanName.family. Sobrenome.</summary>
    public string? Family { get; set; }

    /// <summary>FHIR HumanName.given[]. Nomes próprios (text[] no Postgres).</summary>
    public string[] Given { get; set; } = [];

    /// <summary>FHIR HumanName.prefix[] (Dr., Sra…).</summary>
    public string[] Prefix { get; set; } = [];

    /// <summary>FHIR HumanName.suffix[] (Jr., Filho…).</summary>
    public string[] Suffix { get; set; } = [];

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}
