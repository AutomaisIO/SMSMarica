using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Medicos;

/// <summary>
/// Mapeia o agregado FHIR <see cref="Practitioner"/> + colaterais para o
/// shape <see cref="MedicoDto"/>. Após Fatia 3 do refator FHIR, o "Médico"
/// é simplesmente um Practitioner com Qualification CouncilCode='CRM' e
/// um Usuario(PractitionerId=p.Id) para login.
/// </summary>
internal static class MedicosMapper
{
    internal const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    internal const string SystemCns = "https://fhir.saude.gov.br/sid/cns";
    internal const string SystemRg = "urn:br:gov:rg";
    internal const string SystemRqe = "urn:br:rqe";
    internal const string CouncilCrm = "CRM";

    public static MedicoDto ParaDto(Practitioner p, Usuario? usuario)
    {
        var crm = p.Qualifications.FirstOrDefault(q => q.CouncilCode == CouncilCrm);
        return new MedicoDto(
            p.Id,
            usuario?.Id ?? Guid.Empty,
            NomeOficial(p),
            CpfDe(p) ?? string.Empty,
            p.BirthDate,
            crm?.CouncilNumber ?? string.Empty,
            crm?.CouncilState ?? string.Empty,
            crm?.SpecialtyName,
            RqeDe(p),
            crm?.PeriodEnd,
            TelefonePrincipal(p),
            EnderecoHome(p),
            null, // FotoBase64 — Practitioner não tem coleção de fotos no schema atual
            usuario?.Ativo ?? false,
            p.CreatedAt);
    }

    public static MedicoListItemDto ParaListItem(Practitioner p, Usuario? usuario)
    {
        var crm = p.Qualifications.FirstOrDefault(q => q.CouncilCode == CouncilCrm);
        return new MedicoListItemDto(
            p.Id,
            usuario?.Id ?? Guid.Empty,
            NomeOficial(p),
            CpfDe(p) ?? string.Empty,
            crm?.CouncilNumber ?? string.Empty,
            crm?.CouncilState ?? string.Empty,
            crm?.SpecialtyName,
            null, // FotoBase64
            usuario?.Ativo ?? false);
    }

    internal static string NomeOficial(Practitioner p) =>
        (p.Names.FirstOrDefault(n => n.Use == NameUse.Official) ?? p.Names.FirstOrDefault())?.Text ?? string.Empty;

    internal static string? CpfDe(Practitioner p) =>
        p.Identifiers.FirstOrDefault(i => i.Type == IdentifierTypeCode.Cpf)?.Value;

    internal static string? RqeDe(Practitioner p) =>
        p.Identifiers.FirstOrDefault(i => i.System == SystemRqe)?.Value;

    private static string? TelefonePrincipal(Practitioner p) =>
        p.Telecoms
            .Where(t => t.System == ContactPointSystem.Phone)
            .OrderBy(t => t.Rank ?? int.MaxValue)
            .FirstOrDefault()?.Value;

    private static EnderecoDto? EnderecoHome(Practitioner p)
    {
        var addr = p.Addresses.FirstOrDefault();
        if (addr is null) return null;
        var (logradouro, numero) = SeparaLinha1(addr.Line1);
        return new EnderecoDto(
            addr.PostalCode ?? string.Empty,
            logradouro,
            numero,
            addr.Line2,
            addr.District ?? string.Empty,
            addr.Municipio?.Nome ?? addr.Text ?? string.Empty,
            addr.State ?? string.Empty,
            null);
    }

    private static (string Logradouro, string? Numero) SeparaLinha1(string? linha1)
    {
        if (string.IsNullOrWhiteSpace(linha1)) return (string.Empty, null);
        var idx = linha1.LastIndexOf(',');
        if (idx <= 0) return (linha1.Trim(), null);
        return (linha1[..idx].Trim(), linha1[(idx + 1)..].Trim());
    }
}
