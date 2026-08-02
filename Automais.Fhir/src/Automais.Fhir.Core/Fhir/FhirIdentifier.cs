using Hl7.Fhir.Model;
using Automais.Fhir.Core.Common.Excecoes;

namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// Utilitários do identifier de negócio dos recursos clínicos (conditional update, ADR-0024):
/// o primeiro identifier com system+valor vira coluna de busca e chave do upsert.
/// </summary>
public static class FhirIdentifier
{
    /// <summary>Primeiro identifier completo (system e valor) do recurso; (null, null) se não houver.</summary>
    public static (string? System, string? Value) Primeiro(IEnumerable<Identifier>? identifiers) =>
        identifiers?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.System) && !string.IsNullOrWhiteSpace(i.Value))
            is { } i2 ? (i2.System, i2.Value) : (null, null);

    /// <summary>Interpreta o parâmetro FHIR <c>identifier=system|value</c>; 400 se malformado.</summary>
    public static (string System, string Value) ParseParam(string? identifier)
    {
        var partes = (identifier ?? string.Empty).Split('|', 2);
        if (partes.Length != 2 || string.IsNullOrWhiteSpace(partes[0]) || string.IsNullOrWhiteSpace(partes[1]))
            throw new RecursoInvalidoException("Parâmetro identifier deve ter o formato system|value.");
        return (partes[0].Trim(), partes[1].Trim());
    }

    /// <summary>Garante que o recurso carrega o identifier usado como chave do upsert.</summary>
    public static void Garantir(List<Identifier> identifiers, string system, string value)
    {
        if (!identifiers.Any(i => i.System == system && i.Value == value))
            identifiers.Insert(0, new Identifier(system, value));
    }
}
