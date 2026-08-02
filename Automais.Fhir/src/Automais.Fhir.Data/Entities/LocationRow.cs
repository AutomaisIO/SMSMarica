namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Location</c> (estrutura física do hospital —
/// setor/quarto/leito do Salux, hierarquia via <see cref="PartOfId"/>). Recurso completo
/// em <see cref="ResourceRow.Content"/> (jsonb); colunas abaixo são search params.
/// Estado OPERACIONAL do leito (ocupado/livre agora) fica fora do hub por decisão do
/// ADR-0025 — aqui vive só o cadastro.
/// </summary>
public sealed class LocationRow : ResourceRow
{
    /// <summary>Nome exibível (Location.name — ex.: "CLINICA MEDICA", "Leito 12").</summary>
    public string? Name { get; set; }

    /// <summary>Status cadastral (active/suspended/inactive).</summary>
    public string? Status { get; set; }

    /// <summary>Tipo físico (Location.physicalType.coding[0].code — wa=setor, ro=quarto, bd=leito).</summary>
    public string? PhysicalType { get; set; }

    /// <summary>Pai na hierarquia (Location.partOf = Location/{id}).</summary>
    public Guid? PartOfId { get; set; }

    /// <summary>System do identifier de negócio (urn:salux:unidade/quarto/leito).</summary>
    public string? IdentifierSystem { get; set; }

    /// <summary>Valor do identifier de negócio (prefixado pelo slug da base).</summary>
    public string? IdentifierValue { get; set; }
}
