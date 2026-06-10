namespace SMSMarica.Core.Integracoes.Pep.Fhir;

/// <summary>
/// Falha HTTP ao falar com o hub FHIR, carregando o status para classificação.
/// <see cref="Transitorio"/> = vale a pena reenviar (saturação/indisponibilidade
/// temporária); os demais são permanentes (dado inválido, bug) e não se retentam.
/// </summary>
public sealed class HubFhirHttpException(int statusCode, string corpo, string? contexto = null)
    : Exception($"Hub FHIR retornou {statusCode}{(contexto is null ? "" : $" em {contexto}")}: {corpo}")
{
    public int StatusCode { get; } = statusCode;

    /// <summary>503/502/504/408/429 = retentável (o hub sinaliza "tente de novo").</summary>
    public bool Transitorio => StatusCode is 503 or 502 or 504 or 408 or 429;
}
