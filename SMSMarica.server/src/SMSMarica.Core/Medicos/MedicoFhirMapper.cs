using System.Text.Json;
using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Medicos.Dtos;

namespace SMSMarica.Core.Medicos;

/// <summary>
/// Mapeia entre os DTOs de profissional de saúde do smsmarica e o recurso FHIR
/// <c>Practitioner</c> do hub. Demografia + registro no conselho em campos FHIR
/// nativos (pesquisáveis); restante num extension JSON (mesmo padrão do paciente).
///
/// O registro profissional NÃO é "CRM": é um conselho qualquer (CRM para médico,
/// COREN para enfermagem, CRN para nutrição…). A sigla vai no system do identifier
/// <c>urn:br:conselho:{sigla}:{uf}</c> e no <c>qualification.issuer</c>.
/// </summary>
internal static class MedicoFhirMapper
{
    public const string PayloadUrl = "urn:smsmarica:medico-payload";

    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SystemConselhoRoot = "urn:br:conselho:";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>System do registro: <c>urn:br:conselho:{sigla}:{uf}</c>.</summary>
    public static string SystemConselho(string? sigla, string? uf) =>
        SystemConselhoRoot
        + (sigla ?? string.Empty).Trim().ToLowerInvariant()
        + ":" + (uf ?? string.Empty).Trim().ToUpperInvariant();

    public sealed record Payload(
        string NomeCompleto, string Cpf, DateOnly? DataNascimento,
        string Conselho, string Registro, string UfConselho,
        string? Especialidade, string? Rqe, DateOnly? ValidadeRegistro, string? Telefone,
        EnderecoDto? Endereco, string? FotoBase64);

    public static Practitioner ConstruirNovo(CadastrarMedicoRequest r)
    {
        var pl = new Payload(
            r.NomeCompleto.Trim(), Digitos(r.Cpf), r.DataNascimento,
            NormalizarSigla(r.Conselho), Op(r.Registro) ?? string.Empty,
            (r.UfConselho ?? string.Empty).Trim().ToUpperInvariant(),
            Op(r.Especialidade), Op(r.Rqe), r.ValidadeRegistro, Op(r.Telefone),
            r.Endereco, Op(r.FotoBase64));

        var p = new Practitioner { Active = true };
        AplicarPayload(p, pl);
        return p;
    }

    public static void AplicarAtualizacao(Practitioner existente, AtualizarMedicoRequest r)
    {
        // Médicos importados (Salux) não têm o payload smsmarica — a identidade
        // (nome/CPF/nascimento) vive nos campos FHIR nativos. Sem este fallback,
        // editar zerava o nome e o hub rejeitava o PUT (500).
        var atual = LerPayloadOuNativo(existente);
        var pl = atual with
        {
            Conselho = NormalizarSigla(r.Conselho),
            Registro = Op(r.Registro) ?? string.Empty,
            UfConselho = (r.UfConselho ?? string.Empty).Trim().ToUpperInvariant(),
            Especialidade = Op(r.Especialidade),
            Rqe = Op(r.Rqe),
            ValidadeRegistro = r.ValidadeRegistro,
            Telefone = Op(r.Telefone),
            Endereco = r.Endereco,
            FotoBase64 = Op(r.FotoBase64),
        };
        AplicarPayload(existente, pl);
    }

    public static MedicoDto ParaDto(Practitioner p)
    {
        var pl = LerPayload(p);
        var id = Guid.Parse(p.Id!);
        // Campos FHIR nativos primeiro (profissionais importados — Salux); payload como fallback.
        var (conselho, registro, uf) = ConselhoNativo(p);
        return new MedicoDto(
            id, null, // UsuarioId resolvido por CPF em MedicosService (médico = Practitioner FHIR).
            NomeNativo(p) ?? pl.NomeCompleto,
            IdentValor(p, SystemCpf) ?? pl.Cpf,
            ParseData(p.BirthDate) ?? pl.DataNascimento,
            conselho ?? pl.Conselho, registro ?? pl.Registro, uf ?? pl.UfConselho,
            pl.Especialidade ?? EspecialidadeNativa(p), pl.Rqe, ValidadeNativa(p) ?? pl.ValidadeRegistro,
            TelefoneNativo(p) ?? pl.Telefone, pl.Endereco, pl.FotoBase64,
            p.Active ?? true, p.Meta?.LastUpdated?.UtcDateTime ?? default);
    }

    public static MedicoListItemDto ParaListItem(Practitioner p)
    {
        var pl = LerPayload(p);
        var id = Guid.Parse(p.Id!);
        var (conselho, registro, uf) = ConselhoNativo(p);
        return new MedicoListItemDto(
            id, null, NomeNativo(p) ?? pl.NomeCompleto, IdentValor(p, SystemCpf) ?? pl.Cpf,
            conselho ?? pl.Conselho, registro ?? pl.Registro, uf ?? pl.UfConselho,
            pl.Especialidade ?? EspecialidadeNativa(p), pl.FotoBase64, p.Active ?? true);
    }

    public static string NomeDe(Practitioner p) => NomeNativo(p) ?? LerPayload(p).NomeCompleto;

    private static string? NomeNativo(Practitioner p)
    {
        var n = p.Name?.FirstOrDefault(x => x.Use == HumanName.NameUse.Official)?.Text ?? p.Name?.FirstOrDefault()?.Text;
        return string.IsNullOrWhiteSpace(n) ? null : n;
    }

    private static string? IdentValor(Practitioner p, string system) =>
        p.Identifier?.FirstOrDefault(i => i.System == system)?.Value;

    /// <summary>Extrai (sigla do conselho, número do registro, uf) do identifier <c>urn:br:conselho:{sigla}:{uf}</c>.</summary>
    private static (string? Conselho, string? Registro, string? Uf) ConselhoNativo(Practitioner p)
    {
        var idc = p.Identifier?.FirstOrDefault(i => i.System != null && i.System.StartsWith(SystemConselhoRoot));
        if (idc is null) return (null, null, null);
        var partes = idc.System![SystemConselhoRoot.Length..].Split(':', 2);
        var sigla = string.IsNullOrWhiteSpace(partes[0]) ? null : partes[0].ToUpperInvariant();
        var uf = partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]) ? partes[1].ToUpperInvariant() : null;
        return (sigla, idc.Value, uf);
    }

    private static string? TelefoneNativo(Practitioner p) =>
        p.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value;

    private static DateOnly? ValidadeNativa(Practitioner p) =>
        ParseData(p.Qualification?.FirstOrDefault()?.Period?.End);

    /// <summary>Especialidade(s) trazida(s) do Salux no extension <c>urn:salux:extras</c> (chave <c>especialidade</c>).</summary>
    private static string? EspecialidadeNativa(Practitioner p)
    {
        var raw = (p.GetExtension("urn:salux:extras")?.Value as FhirString)?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("especialidade", out var v) ? Op(v.GetString()) : null;
        }
        catch (JsonException) { return null; }
    }

    private static DateOnly? ParseData(string? d) =>
        DateOnly.TryParse(d, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var r) ? r : null;

    private static void AplicarPayload(Practitioner p, Payload pl)
    {
        p.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = pl.NomeCompleto }];

        var system = SystemConselho(pl.Conselho, pl.UfConselho);

        p.Identifier = [];
        if (!string.IsNullOrWhiteSpace(pl.Cpf)) p.Identifier.Add(new Identifier(SystemCpf, pl.Cpf));
        if (!string.IsNullOrWhiteSpace(pl.Registro)) p.Identifier.Add(new Identifier(system, pl.Registro));

        p.BirthDate = pl.DataNascimento?.ToString("yyyy-MM-dd");

        // Registro no conselho (FHIR Practitioner.qualification): issuer = conselho emissor.
        p.Qualification =
        [
            new Practitioner.QualificationComponent
            {
                Identifier = string.IsNullOrWhiteSpace(pl.Registro) ? null : [new Identifier(system, pl.Registro)],
                Issuer = string.IsNullOrWhiteSpace(pl.Conselho) ? null : new ResourceReference { Display = $"{pl.Conselho}-{pl.UfConselho}" },
                Code = new CodeableConcept { Text = Op(pl.Especialidade) ?? pl.Conselho },
                Period = pl.ValidadeRegistro is null ? null : new Period { End = pl.ValidadeRegistro.Value.ToString("yyyy-MM-dd") },
            },
        ];

        p.RemoveExtension(PayloadUrl);
        p.AddExtension(PayloadUrl, new FhirString(JsonSerializer.Serialize(pl, Json)));
    }

    private static Payload LerPayload(Practitioner p)
    {
        var raw = (p.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        return raw is null
            ? new Payload(string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, null, null, null, null, null, null)
            : JsonSerializer.Deserialize<Payload>(raw, Json)!;
    }

    /// <summary>
    /// Como <see cref="LerPayload"/>, mas quando o Practitioner não tem o payload
    /// smsmarica (ex.: importado do Salux), reconstrói a identidade a partir dos
    /// campos FHIR nativos — evitando zerar nome/CPF ao editar.
    /// </summary>
    private static Payload LerPayloadOuNativo(Practitioner p)
    {
        var raw = (p.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        if (raw is not null) return JsonSerializer.Deserialize<Payload>(raw, Json)!;

        var (conselho, registro, uf) = ConselhoNativo(p);
        return new Payload(
            NomeNativo(p) ?? string.Empty,
            IdentValor(p, SystemCpf) ?? string.Empty,
            ParseData(p.BirthDate),
            conselho ?? "CRM",
            registro ?? string.Empty,
            uf ?? string.Empty,
            EspecialidadeNativa(p),
            null,
            ValidadeNativa(p),
            TelefoneNativo(p),
            null,
            null);
    }

    private static string NormalizarSigla(string? v) =>
        string.IsNullOrWhiteSpace(v) ? "CRM" : v.Trim().ToUpperInvariant();

    private static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new([.. v.Where(char.IsDigit)]);
    private static string? Op(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
