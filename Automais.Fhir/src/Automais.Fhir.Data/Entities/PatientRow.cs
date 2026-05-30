namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Patient</c>. O recurso completo vive em
/// <see cref="ResourceRow.Content"/> (jsonb); as colunas abaixo são apenas
/// search params extraídos do documento para permitir busca indexada sem
/// abrir o JSON.
/// </summary>
public sealed class PatientRow : ResourceRow
{
    /// <summary>CPF (identifier system https://fhir.saude.gov.br/sid/cpf).</summary>
    public string? Cpf { get; set; }

    /// <summary>CNS (identifier system https://fhir.saude.gov.br/sid/cns).</summary>
    public string? Cns { get; set; }

    /// <summary>Nome oficial (name[use=official].text), para busca textual.</summary>
    public string? Nome { get; set; }

    /// <summary>Data de nascimento (birthDate).</summary>
    public DateOnly? Nascimento { get; set; }
}
