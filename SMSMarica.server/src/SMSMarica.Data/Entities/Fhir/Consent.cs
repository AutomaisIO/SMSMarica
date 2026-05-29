using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// FHIR R4 <c>Consent</c>. LGPD: granularidade por tipo + base legal +
/// período + documento assinado. Pode coexistir vários consentimentos por paciente.
/// </summary>
public sealed class Consent
{
    public Guid Id { get; set; }

    public int VersionId { get; set; } = 1;
    public DateTime LastUpdated { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public ConsentStatus Status { get; set; } = ConsentStatus.Active;

    public ConsentType Type { get; set; } = ConsentType.Other;

    /// <summary>Base legal LGPD para o tratamento autorizado.</summary>
    public LgpdLegalBasis LegalBasis { get; set; } = LgpdLegalBasis.ConsentimentoExplicito;

    /// <summary>Data/hora da concessão.</summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>Data/hora da revogação (quando aplicável).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Período de validade (Consent.provision.period).</summary>
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }

    /// <summary>Quem outorgou — titular ou responsável legal (nome).</summary>
    public string? GrantorName { get; set; }

    /// <summary>URL pro termo assinado (S3/MinIO/etc) se houver doc físico.</summary>
    public string? DocumentUrl { get; set; }

    /// <summary>Observação livre.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
