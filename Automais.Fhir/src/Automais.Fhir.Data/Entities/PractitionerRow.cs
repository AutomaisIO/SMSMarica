namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Practitioner</c> (profissional de saúde —
/// médico, enfermeiro). Recurso completo em <see cref="ResourceRow.Content"/>
/// (jsonb); colunas abaixo são search params extraídos.
/// </summary>
public sealed class PractitionerRow : ResourceRow
{
    /// <summary>CPF (identifier system https://fhir.saude.gov.br/sid/cpf).</summary>
    public string? Cpf { get; set; }

    /// <summary>Sigla do conselho profissional emissor: CRM, COREN, CRN, CRO, CRF… (search param).</summary>
    public string? Conselho { get; set; }

    /// <summary>Número do registro no conselho (identifier system urn:br:conselho:{sigla}:{UF}).</summary>
    public string? Registro { get; set; }

    /// <summary>Nome oficial, para busca textual.</summary>
    public string? Nome { get; set; }
}
