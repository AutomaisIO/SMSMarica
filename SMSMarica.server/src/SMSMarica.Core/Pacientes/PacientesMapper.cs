using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Mapeia o agregado <see cref="Patient"/> (FHIR R4 + extensões BR) para o
/// shape <see cref="PacienteDto"/> consumido por <c>SMSMarica.cidadao.app</c>
/// e pelo front. Fatia 1 do refator FHIR: dados clínicos volumosos
/// (alergias, comorbidades, sinais vitais, plano de saúde) ainda não têm
/// home no schema FHIR (vão pra <c>AllergyIntolerance</c>/<c>Condition</c>/
/// <c>Observation</c>/<c>Coverage</c> em fatias futuras) — retornamos vazio
/// por enquanto.
/// </summary>
internal static class PacientesMapper
{
    internal const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    internal const string SystemCns = "https://fhir.saude.gov.br/sid/cns";
    internal const string SystemRg = "urn:br:gov:rg";

    public static PacienteDto ParaDto(Patient p)
    {
        var nomeOficial = NomeOficial(p);
        var nomeSocial = NomeSocial(p);
        var enderecoHome = p.Addresses.FirstOrDefault(a => a.Use == AddressUse.Home)
                         ?? p.Addresses.FirstOrDefault();
        var contatoEmergencia = p.Contacts.FirstOrDefault(c => c.Relationship == PatientContactRelationship.Emergency)
                              ?? p.Contacts.FirstOrDefault();
        var responsavelLegal = p.Contacts.FirstOrDefault(c => c.Relationship == PatientContactRelationship.Guardian);
        var telPrincipal = TelefonePrincipal(p);
        var telCelular = p.Telecoms.FirstOrDefault(t => t.System == ContactPointSystem.Phone && t.Use == ContactPointUse.Mobile)?.Value;
        var telResidencial = p.Telecoms.FirstOrDefault(t => t.System == ContactPointSystem.Phone && t.Use == ContactPointUse.Home && t != telPrincipal)?.Value;
        var email = p.Telecoms.FirstOrDefault(t => t.System == ContactPointSystem.Email)?.Value;
        var foto = p.Photos.FirstOrDefault(f => f.IsPrimary) ?? p.Photos.FirstOrDefault();

        return new PacienteDto(
            p.Id,
            nomeOficial,
            CpfDe(p) ?? string.Empty,
            CnsDe(p),
            (double)(enderecoHome?.Latitude ?? 0m),
            (double)(enderecoHome?.Longitude ?? 0m),
            p.DeletedAt == null,
            p.CreatedAt,
            RgDe(p),
            p.BirthDate,
            DeAdministrativeGender(p.Gender),
            DeMaritalStatus(p.MaritalStatus),
            p.Race,
            DeEducationLevel(p.EducationLevel),
            null, // Ocupacao livre — FHIR usa CBO (OcupacaoCboCodigo). Fatia 1 não expõe.
            p.BirthMunicipio?.Nome,
            p.BirthCountry?.Nome ?? "Brasileira",
            p.MothersMaidenName,
            p.FathersName,
            responsavelLegal?.Name,
            enderecoHome is null ? null : ParaEnderecoDto(enderecoHome),
            telPrincipal?.Value,
            telCelular,
            telResidencial,
            email,
            contatoEmergencia is null ? null : ParaContatoDto(contatoEmergencia),
            null, // AlturaCm — FHIR Observation (sem home Fatia 1)
            null, // PesoKg — idem
            TipoSanguineo.NaoInformado, // FHIR Observation
            FatorRh.NaoInformado, // FHIR Observation
            [], // Alergias — FHIR AllergyIntolerance
            [], // MedicamentosContinuos — FHIR MedicationStatement
            [], // Comorbidades — FHIR Condition
            [.. p.Disabilities.Select(d => d.Description ?? d.Type.ToString())],
            null, // PlanoSaude — FHIR Coverage
            p.Notes,
            foto?.DataBase64,
            nomeSocial);
    }

    public static PacienteListItemDto ParaListItem(Patient p) => new(
        p.Id,
        NomeOficial(p),
        CpfDe(p) ?? string.Empty,
        p.BirthDate,
        p.MothersMaidenName,
        TelefonePrincipal(p)?.Value,
        (p.Photos.FirstOrDefault(f => f.IsPrimary) ?? p.Photos.FirstOrDefault())?.DataBase64,
        p.DeletedAt == null,
        NomeSocial(p));

    public static ContatoEmergenciaDto ParaContatoDto(PatientContact c) => new(
        c.Name,
        c.RelationshipText,
        FormatarTelefone(c.TelephoneDdd, c.TelephoneNumber));

    public static EnderecoDto ParaEnderecoDto(PatientAddress a)
    {
        var (logradouro, numero) = SeparaLinha1(a.Line1);
        return new EnderecoDto(
            a.PostalCode ?? string.Empty,
            logradouro,
            numero,
            a.Line2,
            a.District ?? string.Empty,
            a.Municipio?.Nome ?? string.Empty,
            a.State ?? string.Empty,
            a.ReferencePoint);
    }

    public static PatientContact ParaPatientContact(ContatoEmergenciaDto dto)
    {
        var (ddd, numero) = SeparaTelefone(dto.Telefone);
        return new PatientContact
        {
            Id = Guid.CreateVersion7(),
            Relationship = PatientContactRelationship.Emergency,
            RelationshipText = string.IsNullOrWhiteSpace(dto.Parentesco) ? null : dto.Parentesco.Trim(),
            Name = (dto.Nome ?? string.Empty).Trim(),
            TelephoneDdd = ddd,
            TelephoneNumber = numero,
        };
    }

    public static PatientAddress ParaPatientAddress(EnderecoDto dto) => new()
    {
        Id = Guid.CreateVersion7(),
        Use = AddressUse.Home,
        Type = AddressType.Both,
        Line1 = JuntaLinha1(dto.Logradouro, dto.Numero),
        Line2 = string.IsNullOrWhiteSpace(dto.Complemento) ? null : dto.Complemento.Trim(),
        District = string.IsNullOrWhiteSpace(dto.Bairro) ? null : dto.Bairro.Trim(),
        State = string.IsNullOrWhiteSpace(dto.Uf) ? null : dto.Uf.Trim().ToUpperInvariant(),
        PostalCode = string.IsNullOrWhiteSpace(dto.Cep) ? null : new string([.. dto.Cep.Where(char.IsDigit)]),
        Country = "BRA",
        ReferencePoint = string.IsNullOrWhiteSpace(dto.PontoReferencia) ? null : dto.PontoReferencia.Trim(),
        Text = string.IsNullOrWhiteSpace(dto.Cidade) ? null : dto.Cidade.Trim(),
    };

    // ---- helpers de leitura ----

    private static string NomeOficial(Patient p) =>
        (p.Names.FirstOrDefault(n => n.Use == NameUse.Official) ?? p.Names.FirstOrDefault())?.Text ?? string.Empty;

    private static string? NomeSocial(Patient p) =>
        p.Names.FirstOrDefault(n => n.Use == NameUse.Nickname)?.Text;

    private static string? CpfDe(Patient p) =>
        p.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cpf)?.Value;

    private static string? CnsDe(Patient p) =>
        p.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cns)?.Value;

    private static string? RgDe(Patient p) =>
        p.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Rg)?.Value;

    private static PatientTelecom? TelefonePrincipal(Patient p) =>
        p.Telecoms
            .Where(t => t.System == ContactPointSystem.Phone)
            .OrderBy(t => t.Rank ?? int.MaxValue)
            .FirstOrDefault();

    private static (string Logradouro, string? Numero) SeparaLinha1(string? linha1)
    {
        if (string.IsNullOrWhiteSpace(linha1)) return (string.Empty, null);
        var idx = linha1.LastIndexOf(',');
        if (idx <= 0) return (linha1.Trim(), null);
        return (linha1[..idx].Trim(), linha1[(idx + 1)..].Trim());
    }

    private static string? JuntaLinha1(string? logradouro, string? numero)
    {
        var l = (logradouro ?? string.Empty).Trim();
        var n = (numero ?? string.Empty).Trim();
        if (l.Length == 0 && n.Length == 0) return null;
        return n.Length == 0 ? l : $"{l}, {n}";
    }

    private static string FormatarTelefone(string? ddd, string? numero)
    {
        var d = (ddd ?? string.Empty).Trim();
        var n = (numero ?? string.Empty).Trim();
        if (d.Length == 0 && n.Length == 0) return string.Empty;
        return d.Length == 0 ? n : $"({d}) {n}";
    }

    /// <summary>Separa um telefone "(21) 99999-9999" em DDD e número.</summary>
    private static (string? Ddd, string? Numero) SeparaTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return (null, null);
        var digitos = new string([.. telefone.Where(char.IsDigit)]);
        if (digitos.Length < 10) return (null, digitos.Length == 0 ? null : digitos);
        return (digitos[..2], digitos[2..]);
    }

    // ---- enum mappers (smsmarica ↔ FHIR) ----

    public static AdministrativeGender ParaAdministrativeGender(Sexo? s) => s switch
    {
        Sexo.Masculino => AdministrativeGender.Male,
        Sexo.Feminino => AdministrativeGender.Female,
        Sexo.Outro => AdministrativeGender.Other,
        _ => AdministrativeGender.Unknown,
    };

    public static Sexo DeAdministrativeGender(AdministrativeGender g) => g switch
    {
        AdministrativeGender.Male => Sexo.Masculino,
        AdministrativeGender.Female => Sexo.Feminino,
        AdministrativeGender.Other => Sexo.Outro,
        _ => Sexo.NaoInformado,
    };

    public static MaritalStatus ParaMaritalStatus(EstadoCivil ec) => ec switch
    {
        EstadoCivil.Solteiro => MaritalStatus.NeverMarried,
        EstadoCivil.Casado => MaritalStatus.Married,
        EstadoCivil.UniaoEstavel => MaritalStatus.DomesticPartner,
        EstadoCivil.Divorciado => MaritalStatus.Divorced,
        EstadoCivil.Viuvo => MaritalStatus.Widowed,
        EstadoCivil.Separado => MaritalStatus.LegallySeparated,
        _ => MaritalStatus.Unknown,
    };

    public static EstadoCivil DeMaritalStatus(MaritalStatus m) => m switch
    {
        MaritalStatus.NeverMarried => EstadoCivil.Solteiro,
        MaritalStatus.Married => EstadoCivil.Casado,
        MaritalStatus.DomesticPartner => EstadoCivil.UniaoEstavel,
        MaritalStatus.Divorced => EstadoCivil.Divorciado,
        MaritalStatus.Widowed => EstadoCivil.Viuvo,
        MaritalStatus.LegallySeparated => EstadoCivil.Separado,
        _ => EstadoCivil.NaoInformado,
    };

    public static EducationLevel ParaEducationLevel(Escolaridade e) => e switch
    {
        Escolaridade.SemEscolaridade or Escolaridade.Analfabeto => EducationLevel.SemEscolaridade,
        Escolaridade.FundamentalIncompleto => EducationLevel.FundamentalIncompleto,
        Escolaridade.FundamentalCompleto => EducationLevel.FundamentalCompleto,
        Escolaridade.MedioIncompleto => EducationLevel.MedioIncompleto,
        Escolaridade.MedioCompleto => EducationLevel.MedioCompleto,
        Escolaridade.SuperiorIncompleto => EducationLevel.SuperiorIncompleto,
        Escolaridade.SuperiorCompleto => EducationLevel.SuperiorCompleto,
        Escolaridade.PosGraduacao => EducationLevel.Especializacao,
        _ => EducationLevel.NaoInformado,
    };

    public static Escolaridade DeEducationLevel(EducationLevel e) => e switch
    {
        EducationLevel.SemEscolaridade => Escolaridade.SemEscolaridade,
        EducationLevel.FundamentalIncompleto => Escolaridade.FundamentalIncompleto,
        EducationLevel.FundamentalCompleto => Escolaridade.FundamentalCompleto,
        EducationLevel.MedioIncompleto => Escolaridade.MedioIncompleto,
        EducationLevel.MedioCompleto => Escolaridade.MedioCompleto,
        EducationLevel.SuperiorIncompleto => Escolaridade.SuperiorIncompleto,
        EducationLevel.SuperiorCompleto => Escolaridade.SuperiorCompleto,
        EducationLevel.Especializacao or EducationLevel.Mestrado or EducationLevel.Doutorado => Escolaridade.PosGraduacao,
        _ => Escolaridade.NaoInformado,
    };
}
