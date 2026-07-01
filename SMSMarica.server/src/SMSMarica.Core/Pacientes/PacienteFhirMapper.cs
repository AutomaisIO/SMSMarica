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
            // Imutáveis: pacientes importados (Salux/eSUS) não têm o blob de payload,
            // então LerPayload devolve vazio. Sem isto, AplicarPayload reescreveria o
            // nome com "" (name.text vazio → o hub FHIR rejeita com 400) e apagaria
            // CPF/CNS/nascimento. Preserva do recurso FHIR nativo quando o blob não tem.
            NomeCompleto = string.IsNullOrWhiteSpace(atual.NomeCompleto)
                ? (NomeNativo(existente) ?? atual.NomeCompleto)
                : atual.NomeCompleto,
            Cpf = string.IsNullOrWhiteSpace(atual.Cpf)
                ? (IdentValor(existente, SystemCpf) ?? atual.Cpf)
                : atual.Cpf,
            DataNascimento = atual.DataNascimento ?? ParseData(existente.BirthDate),
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
            Cns = Opcional(r.Cns, true)
                ?? (string.IsNullOrWhiteSpace(atual.Cns) ? IdentValor(existente, SystemCns) : atual.Cns),
        };
        AplicarPayload(existente, payload);
    }

    /// <summary>
    /// Corrige SÓ o nome oficial do paciente, preservando todo o resto
    /// (identificadores, data de nascimento, sexo, contatos, endereço…).
    /// Atualiza o <see cref="HumanName"/> oficial nativo (lido por
    /// <see cref="NomeNativo"/>/<see cref="ParaDto"/>) e, quando existir, o
    /// nome no blob de payload — para manter os dois consistentes.
    ///
    /// CRÍTICO: NÃO chamar <c>AplicarPayload</c>, que recria
    /// Identifier/BirthDate/Gender a partir do payload e apagaria esses dados em
    /// pacientes importados (Salux/eSUS) que não têm o blob.
    /// </summary>
    public static void AplicarNome(Patient existente, string nome)
    {
        var limpo = nome.Trim();

        existente.Name ??= [];
        var oficial = existente.Name.FirstOrDefault(x => x.Use == HumanName.NameUse.Official);
        if (oficial is null)
        {
            oficial = new HumanName { Use = HumanName.NameUse.Official };
            existente.Name.Insert(0, oficial);
        }
        oficial.Text = limpo;
        // Zera partes estruturadas (importadas) para não conflitar com o Text corrigido.
        oficial.Family = null;
        oficial.GivenElement = [];

        // Mantém o blob consistente quando existir; nunca recria a partir de payload vazio.
        var raw = (existente.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        if (raw is not null)
        {
            var pl = JsonSerializer.Deserialize<Payload>(raw, Json)! with { NomeCompleto = limpo };
            existente.RemoveExtension(PayloadUrl);
            existente.AddExtension(PayloadUrl, new FhirString(JsonSerializer.Serialize(pl, Json)));
        }
    }

    public static PacienteDto ParaDto(Patient p)
    {
        var pl = LerPayload(p);
        // Demografia central: campos FHIR nativos primeiro (funciona p/ pacientes
        // de qualquer fonte — Salux, eSUS), payload como fallback dos extras.
        var nome = NomeNativo(p) ?? pl.NomeCompleto;
        var cpf = IdentValor(p, SystemCpf) ?? pl.Cpf ?? string.Empty;
        var cns = IdentValor(p, SystemCns) ?? pl.Cns;
        var rg = IdentValor(p, SystemRg) ?? pl.Rg;
        var nasc = ParseData(p.BirthDate) ?? pl.DataNascimento;
        var sexo = GeneroNativo(p.Gender) ?? pl.Sexo;
        // Endereço/telefone/email/filiação: campos FHIR nativos primeiro (pacientes
        // importados — Salux etc.), payload como fallback (criados pelo smsmarica).
        var mae = ContatoNome(p, "MTH") ?? pl.NomeDaMae;
        var pai = ContatoNome(p, "FTH") ?? pl.NomeDoPai;
        var resp = ContatoNome(p, "GUARD") ?? pl.ResponsavelLegal;
        var endereco = EnderecoNativo(p) ?? pl.Endereco;
        var fonePrinc = TelecomNativo(p, ContactPoint.ContactPointSystem.Phone) ?? pl.TelefonePrincipal;
        var email = TelecomNativo(p, ContactPoint.ContactPointSystem.Email) ?? pl.Email;
        // Tudo do FHIR: todos os identificadores, óbito, cônjuge, fonte e extras crus.
        var identificadores = (p.Identifier ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Value))
            .Select(i => new IdentificadorDto(i.System ?? string.Empty, i.Value!))
            .ToList();
        var obito = ParseData((p.Deceased as FhirDateTime)?.Value);
        var conjuge = ContatoNome(p, "SPS");
        var fonte = p.Meta?.Source;
        var dadosFonte = LerExtras(p);
        return new PacienteDto(
            Guid.Parse(p.Id!), nome, cpf, cns, pl.Latitude, pl.Longitude,
            p.Active ?? true, p.Meta?.LastUpdated?.UtcDateTime ?? default,
            rg, nasc, sexo, pl.EstadoCivil, pl.RacaCor, pl.Escolaridade,
            pl.Ocupacao, pl.Naturalidade, pl.Nacionalidade, mae, pai, resp,
            endereco, fonePrinc, pl.TelefoneCelular, pl.TelefoneResidencial, email,
            pl.ContatoEmergencia, pl.AlturaCm, pl.PesoKg, pl.TipoSanguineo, pl.FatorRh,
            pl.Alergias, pl.MedicamentosContinuos, pl.Comorbidades, pl.Deficiencias, pl.PlanoSaude,
            pl.Observacoes, pl.FotoBase64, pl.NomeSocial,
            identificadores, obito, conjuge, fonte, dadosFonte);
    }

    private static IReadOnlyDictionary<string, string>? LerExtras(Patient p)
    {
        var raw = (p.GetExtension("urn:salux:extras")?.Value as FhirString)?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ToString());
        }
        catch { return null; }
    }

    public static PacienteListItemDto ParaListItem(Patient p)
    {
        var pl = LerPayload(p);
        return new PacienteListItemDto(
            Guid.Parse(p.Id!), NomeNativo(p) ?? pl.NomeCompleto, IdentValor(p, SystemCpf) ?? pl.Cpf ?? string.Empty,
            ParseData(p.BirthDate) ?? pl.DataNascimento, ContatoNome(p, "MTH") ?? pl.NomeDaMae,
            TelecomNativo(p, ContactPoint.ContactPointSystem.Phone) ?? pl.TelefonePrincipal,
            pl.FotoBase64, p.Active ?? true, pl.NomeSocial);
    }

    /// <summary>Nome do paciente (para snapshots em recursos dependentes).</summary>
    public static string NomeDe(Patient p) => NomeNativo(p) ?? LerPayload(p).NomeCompleto;

    private static string? NomeNativo(Patient p)
    {
        var n = p.Name?.FirstOrDefault(x => x.Use == HumanName.NameUse.Official)?.Text
                ?? p.Name?.FirstOrDefault()?.Text;
        return string.IsNullOrWhiteSpace(n) ? null : n;
    }

    private static string? IdentValor(Patient p, string system) =>
        p.Identifier?.FirstOrDefault(i => i.System == system)?.Value;

    private static DateOnly? ParseData(string? d) =>
        DateOnly.TryParse(d, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var r) ? r : null;

    private static Sexo? GeneroNativo(AdministrativeGender? g) => g switch
    {
        AdministrativeGender.Male => Sexo.Masculino,
        AdministrativeGender.Female => Sexo.Feminino,
        _ => null,
    };

    private static string? ContatoNome(Patient p, string code) =>
        p.Contact?.FirstOrDefault(c => c.Relationship != null
            && c.Relationship.Any(r => r.Coding != null && r.Coding.Any(cd => cd.Code == code)))?.Name?.Text;

    private static string? TelecomNativo(Patient p, ContactPoint.ContactPointSystem sistema) =>
        p.Telecom?.FirstOrDefault(t => t.System == sistema)?.Value;

    private static EnderecoDto? EnderecoNativo(Patient p)
    {
        var a = p.Address?.FirstOrDefault();
        if (a is null) return null;
        var linhas = a.Line?.ToList() ?? [];
        var logradouro = linhas.Count > 0 ? linhas[0] : null;
        if (string.IsNullOrWhiteSpace(logradouro) && string.IsNullOrWhiteSpace(a.City)
            && string.IsNullOrWhiteSpace(a.PostalCode))
            return null;
        return new EnderecoDto(
            a.PostalCode ?? string.Empty,
            logradouro ?? string.Empty,
            null,
            linhas.Count > 1 ? linhas[1] : null,
            a.District ?? string.Empty,
            a.City ?? string.Empty,
            a.State ?? string.Empty,
            a.Text);
    }

    private static void AplicarPayload(Patient patient, Payload pl)
    {
        // Nunca emitir HumanName com text vazio (o hub FHIR rejeita com 400). Se o
        // payload não trouxer nome (paciente importado sem blob), preserva o nativo.
        var nomeOficial = string.IsNullOrWhiteSpace(pl.NomeCompleto)
            ? NomeNativo(patient)
            : pl.NomeCompleto.Trim();
        patient.Name = string.IsNullOrWhiteSpace(nomeOficial)
            ? []
            : [new HumanName { Use = HumanName.NameUse.Official, Text = nomeOficial }];
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
