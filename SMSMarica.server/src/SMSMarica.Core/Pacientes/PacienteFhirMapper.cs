using System.Globalization;
using System.Text;
using System.Text.Json;
using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
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

    /// <summary>Marcador de que o paciente já teve o blob promovido para nativo (resumível/idempotente).</summary>
    public const string ExtPromovido = "urn:smsmarica:promovido";
    private const string PromocaoVersaoAtual = "1";

    // Systems canônicos (espelham FhirSystems do hub).
    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SystemCns = "https://fhir.saude.gov.br/sid/cns";
    private const string SystemRg = "urn:br:gov:rg";
    private const string SysV3Marital = "http://terminology.hl7.org/CodeSystem/v3-MaritalStatus";
    private const string ExtHouseNumber = "http://hl7.org/fhir/StructureDefinition/iso21090-ADXP-houseNumber";

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
        // Create pelo painel: o painel é dono de tudo que preencheu (ADR-0020 #1).
        var editados = EditadosNaoVazios(payload);
        if (editados.Count > 0) PatientMergeFhir.MarcarEditados(patient, editados);
        return patient;
    }

    public static void AplicarAtualizacao(Patient existente, AtualizarPacienteRequest r)
    {
        // Diff ANTES de mutar: só os grupos que realmente mudaram viram "editados pelo painel".
        var editados = DiferencaEditados(existente, r);

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
        if (editados.Count > 0) PatientMergeFhir.MarcarEditados(existente, editados);
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

        // Correção de nome pelo painel vence o Oracle no reimport (ADR-0020 #1).
        PatientMergeFhir.MarcarEditados(existente, ["nome"]);
    }

    /// <summary>
    /// Backfill (ADR-0020 R4): promove a demografia do blob para campos FHIR NATIVOS (idempotente).
    /// NÃO marca "campos editados" — não é edição de usuário, só migração de representação. Mantém o
    /// blob (dual-write da Fase A). Devolve false se não há o que fazer (sem blob ou já promovido).
    /// </summary>
    public static bool PromoverBlobParaNativo(Patient p)
    {
        var raw = (p.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        if (raw is null) return false; // sem blob (paciente puro do hub/Salux) — nada a promover
        if ((p.GetExtension(ExtPromovido)?.Value as FhirString)?.Value == PromocaoVersaoAtual) return false;

        var pl = JsonSerializer.Deserialize<Payload>(raw, Json)!;
        AplicarPayload(p, pl); // reescreve nativo a partir do blob (sem marcar editado; mantém o blob)
        p.RemoveExtension(ExtPromovido);
        p.AddExtension(ExtPromovido, new FhirString(PromocaoVersaoAtual));
        return true;
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
        var fonePrinc = TelefonePrincipalNativo(p) ?? pl.TelefonePrincipal;
        var celular = TelefonePorUsoNativo(p, ContactPoint.ContactPointUse.Mobile) ?? pl.TelefoneCelular;
        var residencial = TelefonePorUsoNativo(p, ContactPoint.ContactPointUse.Home) ?? pl.TelefoneResidencial;
        var email = TelecomNativo(p, ContactPoint.ContactPointSystem.Email) ?? pl.Email;
        var estadoCivil = EstadoCivilNativo(p) ?? pl.EstadoCivil;
        var nomeSocial = NomeSocialNativo(p) ?? pl.NomeSocial;
        var (latitude, longitude) = GeolocationNativo(p) ?? (pl.Latitude, pl.Longitude);
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
            Guid.Parse(p.Id!), nome, cpf, cns, latitude, longitude,
            p.Active ?? true, p.Meta?.LastUpdated?.UtcDateTime ?? default,
            rg, nasc, sexo, estadoCivil, pl.RacaCor, pl.Escolaridade,
            pl.Ocupacao, pl.Naturalidade, pl.Nacionalidade, mae, pai, resp,
            endereco, fonePrinc, celular, residencial, email,
            pl.ContatoEmergencia, pl.AlturaCm, pl.PesoKg, pl.TipoSanguineo, pl.FatorRh,
            pl.Alergias, pl.MedicamentosContinuos, pl.Comorbidades, pl.Deficiencias, pl.PlanoSaude,
            pl.Observacoes, pl.FotoBase64, nomeSocial,
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
            TelefonePrincipalNativo(p) ?? pl.TelefonePrincipal,
            pl.FotoBase64, p.Active ?? true, NomeSocialNativo(p) ?? pl.NomeSocial);
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
        var a = p.Address?.FirstOrDefault(x => x.Use == Address.AddressUse.Home) ?? p.Address?.FirstOrDefault();
        if (a is null) return null;
        var linhaEls = a.LineElement?.ToList() ?? [];
        var logradouro = linhaEls.Count > 0 ? linhaEls[0].Value : null;
        if (string.IsNullOrWhiteSpace(logradouro) && string.IsNullOrWhiteSpace(a.City)
            && string.IsNullOrWhiteSpace(a.PostalCode))
            return null;
        // Número: extension iso21090-ADXP-houseNumber no Line[0] (gravado pelo painel).
        var numero = linhaEls.Count > 0
            ? (linhaEls[0].GetExtension(ExtHouseNumber)?.Value as FhirString)?.Value
            : null;
        return new EnderecoDto(
            a.PostalCode ?? string.Empty,
            logradouro ?? string.Empty,
            numero,
            linhaEls.Count > 1 ? linhaEls[1].Value : null,
            a.District ?? string.Empty,
            a.City ?? string.Empty,
            a.State ?? string.Empty,
            a.Text);
    }

    private static (double Latitude, double Longitude)? GeolocationNativo(Patient p)
    {
        var a = p.Address?.FirstOrDefault(x => x.Use == Address.AddressUse.Home) ?? p.Address?.FirstOrDefault();
        var geo = a?.GetExtension(PatientMergeFhir.ExtGeolocation);
        if (geo is null) return null;
        var lat = (geo.GetExtension("latitude")?.Value as FhirDecimal)?.Value;
        var lng = (geo.GetExtension("longitude")?.Value as FhirDecimal)?.Value;
        return lat is null || lng is null ? null : ((double)lat.Value, (double)lng.Value);
    }

    private static string? NomeSocialNativo(Patient p)
    {
        var s = p.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Nickname)?.Text;
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static string? TelefonePrincipalNativo(Patient p)
    {
        var fones = p.Telecom?.Where(t => t.System == ContactPoint.ContactPointSystem.Phone).ToList() ?? [];
        var princ = fones.FirstOrDefault(t => t.Rank == 1) ?? fones.FirstOrDefault();
        return string.IsNullOrWhiteSpace(princ?.Value) ? null : princ.Value;
    }

    private static string? TelefonePorUsoNativo(Patient p, ContactPoint.ContactPointUse uso)
    {
        var fones = p.Telecom?.Where(t => t.System == ContactPoint.ContactPointSystem.Phone).ToList() ?? [];
        var princ = fones.FirstOrDefault(t => t.Rank == 1) ?? fones.FirstOrDefault();
        // Exclui o principal POR REFERÊNCIA (não por dígitos): permite que celular/residencial
        // com o mesmo número do principal ainda sejam representados/lidos distintamente.
        var alvo = fones.FirstOrDefault(t => t.Use == uso && !ReferenceEquals(t, princ));
        return string.IsNullOrWhiteSpace(alvo?.Value) ? null : alvo.Value;
    }

    private static EstadoCivil? EstadoCivilNativo(Patient p)
    {
        var mc = p.MaritalStatus;
        if (mc is null) return null;
        var code = mc.Coding?.FirstOrDefault(c => c.System == SysV3Marital)?.Code;
        var porCode = code switch
        {
            "S" => EstadoCivil.Solteiro,
            "M" => EstadoCivil.Casado,
            "T" => EstadoCivil.UniaoEstavel,
            "D" => EstadoCivil.Divorciado,
            "W" => EstadoCivil.Viuvo,
            "L" => EstadoCivil.Separado,
            _ => (EstadoCivil?)null,
        };
        return porCode ?? NormalizarEstadoCivilTexto(mc.Text);
    }

    /// <summary>Normaliza o texto pt-BR do estado civil (importado grava só .text, ex.: "CASADO(A)").</summary>
    private static EstadoCivil? NormalizarEstadoCivilTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var n = new string(texto.Trim().ToUpperInvariant()
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        n = n.Replace("(A)", string.Empty).Replace("(O)", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty).Trim();
        if (n.StartsWith("SOLTEIR", StringComparison.Ordinal)) return EstadoCivil.Solteiro;
        if (n.StartsWith("CASAD", StringComparison.Ordinal)) return EstadoCivil.Casado;
        if (n.Contains("UNIAO", StringComparison.Ordinal)) return EstadoCivil.UniaoEstavel;
        if (n.StartsWith("DIVORCIAD", StringComparison.Ordinal)) return EstadoCivil.Divorciado;
        if (n.StartsWith("VIUV", StringComparison.Ordinal)) return EstadoCivil.Viuvo;
        if (n.StartsWith("SEPARAD", StringComparison.Ordinal)) return EstadoCivil.Separado;
        return null;
    }

    private static void AplicarPayload(Patient patient, Payload pl)
    {
        // ESCRITA NATIVA (fonte da verdade), por MERGE/upsert — mesmo shape do import
        // (SaluxFhirMapper). Preserva identificadores/campos não geridos; nunca replace-all.
        // Nome vazio (paciente importado sem blob) preserva o nativo (evita name.text="" → 400).
        PatientMergeFhir.UpsertNomeOficial(patient,
            string.IsNullOrWhiteSpace(pl.NomeCompleto) ? NomeNativo(patient) : pl.NomeCompleto);
        PatientMergeFhir.UpsertNomeSocial(patient, pl.NomeSocial);
        PatientMergeFhir.UpsertIdentifier(patient, SystemCpf, pl.Cpf);
        PatientMergeFhir.UpsertIdentifier(patient, SystemCns, pl.Cns);
        PatientMergeFhir.UpsertIdentifier(patient, SystemRg, pl.Rg);
        PatientMergeFhir.SetBirthDate(patient, pl.DataNascimento);
        PatientMergeFhir.SetGender(patient, pl.Sexo);
        PatientMergeFhir.SetMaritalStatus(patient, pl.EstadoCivil);
        PatientMergeFhir.UpsertEndereco(patient, pl.Endereco);
        PatientMergeFhir.AplicarContatos(patient, pl.TelefonePrincipal, pl.TelefoneCelular,
            pl.TelefoneResidencial, pl.Email);
        PatientMergeFhir.UpsertContato(patient, "MTH", pl.NomeDaMae);
        PatientMergeFhir.UpsertContato(patient, "FTH", pl.NomeDoPai);
        PatientMergeFhir.UpsertContato(patient, "GUARD", pl.ResponsavelLegal);

        // Dual-write do blob (Fase A: rede de segurança / reversibilidade; sai na Fase C).
        // Foto, geolocalização (lat/long) e contato de emergência ainda vivem só no blob
        // nesta fase — ParaDto os lê do blob (ver ADR-0020).
        patient.RemoveExtension(PayloadUrl);
        patient.AddExtension(PayloadUrl, new FhirString(JsonSerializer.Serialize(pl, Json)));
        // O carimbo de "campos editados" (ADR-0020 #1) NÃO é feito aqui (AplicarPayload é
        // compartilhado por create/update): create marca tudo o que foi preenchido; update marca
        // só o que MUDOU vs o nativo — ver ConstruirNovo/AplicarAtualizacao.
    }

    /// <summary>Grupos de campo preenchidos (não-vazios) no payload — usado no CREATE (painel dono).</summary>
    private static List<string> EditadosNaoVazios(Payload pl)
    {
        var ed = new List<string>();
        if (!string.IsNullOrWhiteSpace(pl.TelefonePrincipal) || !string.IsNullOrWhiteSpace(pl.TelefoneCelular)
            || !string.IsNullOrWhiteSpace(pl.TelefoneResidencial)) ed.Add("telefone");
        if (!string.IsNullOrWhiteSpace(pl.Email)) ed.Add("email");
        if (pl.Endereco is not null && (!string.IsNullOrWhiteSpace(pl.Endereco.Logradouro)
            || !string.IsNullOrWhiteSpace(pl.Endereco.Cidade))) ed.Add("endereco");
        if (!string.IsNullOrWhiteSpace(pl.NomeSocial)) ed.Add("nomeSocial");
        if (pl.EstadoCivil != EstadoCivil.NaoInformado) ed.Add("estadoCivil");
        if (!string.IsNullOrWhiteSpace(pl.NomeDaMae) || !string.IsNullOrWhiteSpace(pl.NomeDoPai)
            || !string.IsNullOrWhiteSpace(pl.ResponsavelLegal)) ed.Add("filiacao");
        return ed;
    }

    /// <summary>
    /// Grupos que MUDARAM no update vs o estado nativo atual — só esses "o painel venceu"
    /// (ADR-0020 #1). Evita congelar TODO o demográfico contra o Oracle a cada save.
    /// </summary>
    private static List<string> DiferencaEditados(Patient p, AtualizarPacienteRequest r)
    {
        var ed = new List<string>();
        if (Digitos(r.TelefonePrincipal) != Digitos(TelefonePrincipalNativo(p))
            || Digitos(r.TelefoneCelular) != Digitos(TelefonePorUsoNativo(p, ContactPoint.ContactPointUse.Mobile))
            || Digitos(r.TelefoneResidencial) != Digitos(TelefonePorUsoNativo(p, ContactPoint.ContactPointUse.Home)))
            ed.Add("telefone");
        if (NormEmail(r.Email) != NormEmail(TelecomNativo(p, ContactPoint.ContactPointSystem.Email))) ed.Add("email");
        if (!EnderecoIgual(r.Endereco, EnderecoNativo(p))) ed.Add("endereco");
        if (r.EstadoCivil != (EstadoCivilNativo(p) ?? EstadoCivil.NaoInformado)) ed.Add("estadoCivil");
        if (Norm(r.NomeSocial) != Norm(NomeSocialNativo(p))) ed.Add("nomeSocial");
        if (Norm(r.NomeDaMae) != Norm(ContatoNome(p, "MTH")) || Norm(r.NomeDoPai) != Norm(ContatoNome(p, "FTH"))
            || Norm(r.ResponsavelLegal) != Norm(ContatoNome(p, "GUARD"))) ed.Add("filiacao");
        return ed;
    }

    private static string Norm(string? v) => v?.Trim() ?? string.Empty;
    private static string NormEmail(string? v) => v?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool EnderecoIgual(EnderecoDto? a, EnderecoDto? b)
    {
        static string S(EnderecoDto? e) => e is null ? string.Empty : string.Join('|',
            Norm(e.Logradouro), Norm(e.Numero), Norm(e.Complemento), Digitos(e.Cep),
            Norm(e.Bairro), Norm(e.Cidade), Norm(e.Uf).ToUpperInvariant(), Norm(e.PontoReferencia));
        return S(a) == S(b);
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
