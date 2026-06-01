using System.Text.Json;
using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Medicos.Dtos;

namespace SMSMarica.Core.Medicos;

/// <summary>
/// Mapeia entre os DTOs de médico do smsmarica e o recurso FHIR
/// <c>Practitioner</c> do hub. Demografia/CRM em campos FHIR nativos
/// (pesquisáveis); restante num extension JSON (mesmo padrão do paciente).
/// </summary>
internal static class MedicoFhirMapper
{
    public const string PayloadUrl = "urn:smsmarica:medico-payload";

    private const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SystemCrmPrefix = "urn:br:conselho:crm:";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string SystemCrm(string uf) => SystemCrmPrefix + (uf ?? string.Empty).Trim().ToUpperInvariant();

    public sealed record Payload(
        string NomeCompleto, string Cpf, DateOnly? DataNascimento, string Crm, string UfCrm,
        string? Especialidade, string? Rqe, DateOnly? ValidadeCrm, string? Telefone,
        EnderecoDto? Endereco, string? FotoBase64);

    public static Practitioner ConstruirNovo(CadastrarMedicoRequest r)
    {
        var pl = new Payload(
            r.NomeCompleto.Trim(), Digitos(r.Cpf), r.DataNascimento, Digitos(r.Crm),
            (r.UfCrm ?? string.Empty).Trim().ToUpperInvariant(), Op(r.Especialidade), Op(r.Rqe),
            r.ValidadeCrm, Op(r.Telefone), r.Endereco, Op(r.FotoBase64));

        var p = new Practitioner { Active = true };
        AplicarPayload(p, pl);
        return p;
    }

    public static void AplicarAtualizacao(Practitioner existente, AtualizarMedicoRequest r)
    {
        var atual = LerPayload(existente);
        var pl = atual with
        {
            Crm = Digitos(r.Crm),
            UfCrm = (r.UfCrm ?? string.Empty).Trim().ToUpperInvariant(),
            Especialidade = Op(r.Especialidade),
            Rqe = Op(r.Rqe),
            ValidadeCrm = r.ValidadeCrm,
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
        // Campos FHIR nativos primeiro (médicos importados — Salux); payload como fallback.
        var (crm, uf) = CrmNativo(p);
        return new MedicoDto(
            id, id,
            NomeNativo(p) ?? pl.NomeCompleto,
            IdentValor(p, SystemCpf) ?? pl.Cpf,
            ParseData(p.BirthDate) ?? pl.DataNascimento,
            crm ?? pl.Crm, uf ?? pl.UfCrm,
            pl.Especialidade, pl.Rqe, ValidadeNativa(p) ?? pl.ValidadeCrm,
            TelefoneNativo(p) ?? pl.Telefone, pl.Endereco, pl.FotoBase64,
            p.Active ?? true, p.Meta?.LastUpdated?.UtcDateTime ?? default);
    }

    public static MedicoListItemDto ParaListItem(Practitioner p)
    {
        var pl = LerPayload(p);
        var id = Guid.Parse(p.Id!);
        var (crm, uf) = CrmNativo(p);
        return new MedicoListItemDto(
            id, id, NomeNativo(p) ?? pl.NomeCompleto, IdentValor(p, SystemCpf) ?? pl.Cpf,
            crm ?? pl.Crm, uf ?? pl.UfCrm, pl.Especialidade, pl.FotoBase64, p.Active ?? true);
    }

    public static string NomeDe(Practitioner p) => NomeNativo(p) ?? LerPayload(p).NomeCompleto;
    public static (string Crm, string Uf) CrmDe(Practitioner p)
    {
        var (crm, uf) = CrmNativo(p);
        var pl = LerPayload(p);
        return (crm ?? pl.Crm, uf ?? pl.UfCrm);
    }

    private static string? NomeNativo(Practitioner p)
    {
        var n = p.Name?.FirstOrDefault(x => x.Use == HumanName.NameUse.Official)?.Text ?? p.Name?.FirstOrDefault()?.Text;
        return string.IsNullOrWhiteSpace(n) ? null : n;
    }

    private static string? IdentValor(Practitioner p, string system) =>
        p.Identifier?.FirstOrDefault(i => i.System == system)?.Value;

    private static (string? Crm, string? Uf) CrmNativo(Practitioner p)
    {
        var idc = p.Identifier?.FirstOrDefault(i => i.System != null && i.System.StartsWith(SystemCrmPrefix));
        if (idc is null) return (null, null);
        return (idc.Value, idc.System!.Length > SystemCrmPrefix.Length ? idc.System![SystemCrmPrefix.Length..] : null);
    }

    private static string? TelefoneNativo(Practitioner p) =>
        p.Telecom?.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value;

    private static DateOnly? ValidadeNativa(Practitioner p) =>
        ParseData(p.Qualification?.FirstOrDefault()?.Period?.End);

    private static DateOnly? ParseData(string? d) =>
        DateOnly.TryParse(d, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var r) ? r : null;

    private static void AplicarPayload(Practitioner p, Payload pl)
    {
        p.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = pl.NomeCompleto }];

        p.Identifier = [];
        if (!string.IsNullOrWhiteSpace(pl.Cpf)) p.Identifier.Add(new Identifier(SystemCpf, pl.Cpf));
        if (!string.IsNullOrWhiteSpace(pl.Crm)) p.Identifier.Add(new Identifier(SystemCrm(pl.UfCrm), pl.Crm));

        p.BirthDate = pl.DataNascimento?.ToString("yyyy-MM-dd");

        // Qualificação CRM (FHIR Practitioner.qualification).
        p.Qualification =
        [
            new Practitioner.QualificationComponent
            {
                Identifier = string.IsNullOrWhiteSpace(pl.Crm) ? null : [new Identifier(SystemCrm(pl.UfCrm), pl.Crm)],
                Code = new CodeableConcept { Text = "CRM" + (string.IsNullOrWhiteSpace(pl.Especialidade) ? "" : " - " + pl.Especialidade) },
                Period = pl.ValidadeCrm is null ? null : new Period { End = pl.ValidadeCrm.Value.ToString("yyyy-MM-dd") },
            },
        ];

        p.RemoveExtension(PayloadUrl);
        p.AddExtension(PayloadUrl, new FhirString(JsonSerializer.Serialize(pl, Json)));
    }

    private static Payload LerPayload(Practitioner p)
    {
        var raw = (p.GetExtension(PayloadUrl)?.Value as FhirString)?.Value;
        return raw is null
            ? new Payload(string.Empty, string.Empty, null, string.Empty, string.Empty, null, null, null, null, null, null)
            : JsonSerializer.Deserialize<Payload>(raw, Json)!;
    }

    private static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new([.. v.Where(char.IsDigit)]);
    private static string? Op(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
