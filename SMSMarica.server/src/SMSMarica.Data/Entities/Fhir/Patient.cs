using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Fhir.Enums;
using SMSMarica.Data.Entities.Fhir.Lookups;

namespace SMSMarica.Data.Entities.Fhir;

/// <summary>
/// Recurso FHIR R4 <c>Patient</c> (perfil BRIndividuo da RNDS quando aplicável).
/// Centro de identidade do cidadão. Identificadores (CPF/CNS/RG), nomes,
/// endereços, telecoms, contatos de emergência, vínculos de fusão e fotos
/// vivem em tabelas-filhas separadas (FHIR multivalorados).
/// </summary>
public sealed class Patient
{
    public Guid Id { get; set; }

    // ---- meta ----
    /// <summary>FHIR Resource.meta.versionId. Incrementa a cada update.</summary>
    public int VersionId { get; set; } = 1;
    /// <summary>FHIR Resource.meta.lastUpdated.</summary>
    public DateTime LastUpdated { get; set; }

    // ---- Patient base ----
    /// <summary>FHIR Patient.active. Default true.</summary>
    public bool Active { get; set; } = true;

    /// <summary>FHIR Patient.gender (administrativo).</summary>
    public AdministrativeGender Gender { get; set; } = AdministrativeGender.Unknown;

    /// <summary>FHIR Patient.birthDate.</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Extensão data-absent-reason: marca BirthDate como estimada.</summary>
    public bool BirthDateEstimated { get; set; }

    /// <summary>FHIR Patient.deceasedBoolean (ou true quando DeceasedDateTime != null).</summary>
    public bool DeceasedBoolean { get; set; }

    /// <summary>FHIR Patient.deceasedDateTime.</summary>
    public DateTime? DeceasedDateTime { get; set; }

    /// <summary>Extensão: óbito presumido (sem certidão).</summary>
    public bool DeceasedPresumed { get; set; }

    /// <summary>FHIR Patient.maritalStatus.</summary>
    public MaritalStatus MaritalStatus { get; set; } = MaritalStatus.Unknown;

    /// <summary>FHIR Patient.multipleBirthBoolean.</summary>
    public bool MultipleBirthBoolean { get; set; }

    /// <summary>FHIR Patient.multipleBirthInteger (ordem nascimento se gemelar).</summary>
    public int? MultipleBirthInteger { get; set; }

    /// <summary>FHIR Patient.managingOrganization (CNES da unidade que cadastrou).</summary>
    public Guid? ManagingOrganizationId { get; set; }
    public Organization? ManagingOrganization { get; set; }

    // ---- Extensões BR (perfil BRIndividuo + eSUS APS) ----

    /// <summary>Raça/cor autodeclarada (DataSUS — extensão br-individual-race).</summary>
    public RacaCor Race { get; set; } = RacaCor.NaoInformado;

    /// <summary>Etnia indígena (obrigatório quando Race=Indigena).</summary>
    public int? EtniaIndigenaCodigo { get; set; }
    public EtniaIndigena? EtniaIndigena { get; set; }

    /// <summary>Extensão mothersMaidenName. Obrigatório no perfil BR.</summary>
    public string? MothersMaidenName { get; set; }

    /// <summary>Nome do pai (não tem FHIR padrão — extensão local).</summary>
    public string? FathersName { get; set; }

    /// <summary>Extensão birthCountry. País de nascimento (default BRA).</summary>
    public string? BirthCountryCode { get; set; }
    public PaisIso? BirthCountry { get; set; }

    /// <summary>Extensão birthPlace.municipio. Naturalidade (município IBGE).</summary>
    public int? BirthMunicipioCodigo { get; set; }
    public MunicipioIbge? BirthMunicipio { get; set; }

    /// <summary>Data de entrada no país (estrangeiros).</summary>
    public DateOnly? CountryEntryDate { get; set; }

    /// <summary>Religião autodeclarada (extensão patient-religion).</summary>
    public int? ReligiaoCodigo { get; set; }
    public Religiao? Religiao { get; set; }

    /// <summary>Ocupação CBO (extensão eSUS).</summary>
    public int? OcupacaoCboCodigo { get; set; }
    public CboOcupacao? OcupacaoCbo { get; set; }

    /// <summary>Escolaridade (eSUS APS / Portaria 1.434).</summary>
    public EducationLevel EducationLevel { get; set; } = EducationLevel.NaoInformado;

    /// <summary>Frequenta escola (eSUS APS).</summary>
    public bool? AttendsSchool { get; set; }

    /// <summary>Identidade de gênero (separada do sexo administrativo).</summary>
    public GenderIdentity GenderIdentity { get; set; } = GenderIdentity.NaoInformado;

    /// <summary>Flag "usar nome social na chamada" — paciente prefere nome social.</summary>
    public bool UseSocialName { get; set; }

    /// <summary>Paciente sem documentação (indigente/desconhecido).</summary>
    public bool HasNoDocumentation { get; set; }

    /// <summary>Anotações livres (Patient.text Narrative).</summary>
    public string? Notes { get; set; }

    // ---- Auditoria + LGPD ----
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Base legal LGPD principal do tratamento desse cadastro.</summary>
    public LgpdLegalBasis LegalBasis { get; set; } = LgpdLegalBasis.PoliticaPublicaSus;

    // ---- Coleções (FHIR multivalorados) ----
    public ICollection<PatientIdentifier> Identifiers { get; set; } = [];
    public ICollection<PatientName> Names { get; set; } = [];
    public ICollection<PatientAddress> Addresses { get; set; } = [];
    public ICollection<PatientTelecom> Telecoms { get; set; } = [];
    public ICollection<PatientContact> Contacts { get; set; } = [];
    public ICollection<PatientCommunication> Communications { get; set; } = [];
    public ICollection<PatientLink> Links { get; set; } = [];
    public ICollection<PatientPhoto> Photos { get; set; } = [];
    public ICollection<PatientDisability> Disabilities { get; set; } = [];
    public ICollection<Consent> Consents { get; set; } = [];
}
