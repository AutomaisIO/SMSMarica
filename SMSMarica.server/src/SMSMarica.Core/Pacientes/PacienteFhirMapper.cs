using System.Text.Json;
using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Mapeia entre os DTOs do smsmarica e o recurso FHIR <c>Patient</c> do hub.
///
/// MVP/tech-debt: os campos demográficos centrais (nome, CPF, CNS, RG, data de
/// nascimento, sexo) vão para campos FHIR nativos (pesquisáveis); o restante do
/// shape do PacienteDto é guardado, sem perda, num extension JSON
/// (<see cref="PayloadUrl"/>). Numa fatia futura esses extras viram recursos
/// FHIR próprios (Observation, AllergyIntolerance, etc.) e o blob sai.
/// </summary>
internal static class PacienteFhirMapper
{
    public const string PayloadUrl = "urn:smsmarica:paciente-payload";

    // Systems canônicos (espelham FhirSystems do hub).
    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SystemCns = "https://fhir.saude.gov.br/sid/cns";
    private const string SystemRg = "urn:br:gov:rg";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Dados do paciente que não são demográficos centrais — guardados como blob.</summary>
    public sealed record Payload(
        string NomeCompleto,
        string Cpf,
        string? Cns,
        string? Rg,
        DateOnly? DataNascimento,
        Sexo Sexo,
        double Latitude,
        double Longitude,
        EstadoCivil EstadoCivil,
        RacaCor RacaCor,
        Escolaridade Escolaridade,
        string? Ocupacao,
        string? Naturalidade,
        string Nacionalidade,
        string? NomeDaMae,
        string? NomeDoPai,
        string? ResponsavelLegal,
        EnderecoDto? Endereco,
        string? TelefonePrincipal,
        string? TelefoneCelular,
        string? TelefoneResidencial,
        string? Email,
        ContatoEmergenciaDto? ContatoEmergencia,
        int? AlturaCm,
        decimal? PesoKg,
        TipoSanguineo TipoSanguineo,
        FatorRh FatorRh,
        IReadOnlyList<string> Alergias,
        IReadOnlyList<string> MedicamentosContinuos,
        IReadOnlyList<string> Comorbidades,
        IReadOnlyList<string> Deficiencias,
        string? PlanoSaude,
        string? Observacoes,
        string? FotoBase64,
        string? NomeSocial);

    public static Patient ConstruirNovo(CadastrarPacienteRequest r)
    {
        var payload = new Payload(
            r.NomeCompleto.Trim(), Digitos(r.Cpf), Opcional(r.Cns, true), Opcional(r.Rg, false),
            r.DataNascimento, r.Sexo, 0, 0, r.EstadoCivil, r.RacaCor, r.Escolaridade,
            Opcional(r.Ocupacao, false), Opcional(r.Naturalidade, false),
            string.IsNullOrWhiteSpace(r.Nacionalidade) ? "Brasileira" : r.Nacionalidade!.Trim(),
            Opcional(r.NomeDaMae, false), Opcional(r.NomeDoPai, false), Opcional(r.ResponsavelLegal, false),
            r.Endereco, Opcional(r.TelefonePrincipal, false), Opcional(r.TelefoneCelular, false),
            Opcional(r.TelefoneResidencial, false), Opcional(r.Email, false), r.ContatoEmergencia,
            r.AlturaCm, r.PesoKg, r.TipoSanguineo, r.FatorRh, Lista(r.Alergias), Lista(r.MedicamentosContinuos),
            Lista(r.Comorbidades), Lista(r.Deficiencias), Opcional(r.PlanoSaude, false),
            Opcional(r.Observacoes, false), Opcional(r.FotoBase64, false), Opcional(r.NomeSocial, false));

        var patient = new Patient { Active = true };
        AplicarPayload(patient, payload);
        return patient;
    }

    public static void AplicarAtualizacao(Patient existente, AtualizarPacienteRequest r)
    {
        // Nome, CPF, CNS e data de nascimento são imutáveis — preservados do existente.
        var atual = LerPayload(existente);
        var payload = atual with
        {
            Rg = Opcional(r.Rg, false),
            Sexo = r.Sexo,
            EstadoCivil = r.EstadoCivil,
            RacaCor = r.RacaCor,
            Escolaridade = r.Escolaridade,
            Ocupacao = Opcional(r.Ocupacao, false),
            Naturalidade = Opcional(r.Naturalidade, false),
            Nacionalidade = string.IsNullOrWhiteSpace(r.Nacionalidade) ? "Brasileira" : r.Nacionalidade!.Trim(),
            NomeDaMae = Opcional(r.NomeDaMae, false),
            NomeDoPai = Opcional(r.NomeDoPai, false),
            ResponsavelLegal = Opcional(r.ResponsavelLegal, false),
            Endereco = r.Endereco,
            TelefonePrincipal = Opcional(r.TelefonePrincipal, false),
            TelefoneCelular = Opcional(r.TelefoneCelular, false),
            TelefoneResidencial = Opcional(r.TelefoneResidencial, false),
            Email = string.IsNullOrWhiteSpace(r.Email) ? atual.Email : r.Email.Trim().ToLowerInvariant(),
            ContatoEmergencia = r.ContatoEmergencia,
            AlturaCm = r.AlturaCm,
            PesoKg = r.PesoKg,
            TipoSanguineo = r.TipoSanguineo,
            FatorRh = r.FatorRh,
            Alergias = Lista(r.Alergias),
            MedicamentosContinuos = Lista(r.MedicamentosContinuos),
            Comorbidades = Lista(r.Comorbidades),
            Deficiencias = Lista(r.Deficiencias),
            PlanoSaude = Opcional(r.PlanoSaude, false),
            Observacoes = Opcional(r.Observacoes, false),
            FotoBase64 = Opcional(r.FotoBase64, false),
            NomeSocial = Opcional(r.NomeSocial, false),
            Cns = Opcional(r.Cns, true) ?? atual.Cns,
        };
        AplicarPayload(existente, payload);
    }

    public static PacienteDto ParaDto(Patient p)
    {
        var pl = LerPayload(p);
        return new PacienteDto(
            Guid.Parse(p.Id!), pl.NomeCompleto, pl.Cpf, pl.Cns, pl.Latitude, pl.Longitude,
            p.Active ?? true, p.Meta?.LastUpdated?.UtcDateTime ?? default,
            pl.Rg, pl.DataNascimento, pl.Sexo, pl.EstadoCivil, pl.RacaCor, pl.Escolaridade,
            pl.Ocupacao, pl.Naturalidade, pl.Nacionalidade, pl.NomeDaMae, pl.NomeDoPai, pl.ResponsavelLegal,
            pl.Endereco, pl.TelefonePrincipal, pl.TelefoneCelular, pl.TelefoneResidencial, pl.Email,
            pl.ContatoEmergencia, pl.AlturaCm, pl.PesoKg, pl.TipoSanguineo, pl.FatorRh,
            pl.Alergias, pl.MedicamentosContinuos, pl.Comorbidades, pl.Deficiencias, pl.PlanoSaude,
            pl.Observacoes, pl.FotoBase64, pl.NomeSocial);
    }

    public static PacienteListItemDto ParaListItem(Patient p)
    {
        var pl = LerPayload(p);
        return new PacienteListItemDto(
            Guid.Parse(p.Id!), pl.NomeCompleto, pl.Cpf, pl.DataNascimento, pl.NomeDaMae,
            pl.TelefonePrincipal, pl.FotoBase64, p.Active ?? true, pl.NomeSocial);
    }

    /// <summary>Nome do paciente (para snapshots em recursos dependentes).</summary>
    public static string NomeDe(Patient p) => LerPayload(p).NomeCompleto;

    private static void AplicarPayload(Patient patient, Payload pl)
    {
        patient.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = pl.NomeCompleto }];
        if (!string.IsNullOrWhiteSpace(pl.NomeSocial))
            patient.Name.Add(new HumanName { Use = HumanName.NameUse.Nickname, Text = pl.NomeSocial });

        patient.Identifier = [];
        if (!string.IsNullOrWhiteSpace(pl.Cpf)) patient.Identifier.Add(new Identifier(SystemCpf, pl.Cpf));
        if (!string.IsNullOrWhiteSpace(pl.Cns)) patient.Identifier.Add(new Identifier(SystemCns, pl.Cns));
        if (!string.IsNullOrWhiteSpace(pl.Rg)) patient.Identifier.Add(new Identifier(SystemRg, pl.Rg));

        patient.BirthDate = pl.DataNascimento?.ToString("yyyy-MM-dd");
        patient.Gender = pl.Sexo switch
        {
            Sexo.Masculino => AdministrativeGender.Male,
            Sexo.Feminino => AdministrativeGender.Female,
            _ => AdministrativeGender.Unknown,
        };

        patient.RemoveExtension(PayloadUrl);
        patient.AddExtension(PayloadUrl, new FhirString(JsonSerializer.Serialize(pl, Json)));
    }

    private static Payload LerPayload(Patient p)
    {
        var raw = (p.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        return raw is null
            ? new Payload(string.Empty, string.Empty, null, null, null, Sexo.NaoInformado, 0, 0,
                EstadoCivil.NaoInformado, RacaCor.NaoInformado, Escolaridade.NaoInformado, null, null,
                "Brasileira", null, null, null, null, null, null, null, null, null, null, null,
                TipoSanguineo.NaoInformado, FatorRh.NaoInformado, [], [], [], [], null, null, null, null)
            : JsonSerializer.Deserialize<Payload>(raw, Json)!;
    }

    private static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new([.. v.Where(char.IsDigit)]);

    private static string? Opcional(string? v, bool soDigitos)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return soDigitos ? Digitos(t) : t;
    }

    private static List<string> Lista(IReadOnlyList<string>? l) =>
        l is null ? [] : [.. l.Select(x => x?.Trim() ?? string.Empty).Where(x => x.Length > 0)];
}
