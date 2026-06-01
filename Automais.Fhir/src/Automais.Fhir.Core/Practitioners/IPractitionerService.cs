using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Practitioners;

/// <summary>Filtros de busca de Practitioner.</summary>
/// <param name="Conselho">Sigla exata do conselho (CRM, COREN…) — usado pelos menus por categoria.</param>
/// <param name="ConselhoDiferenteDe">Exclui uma sigla (ex.: "todos os profissionais menos CRM").</param>
public sealed record PractitionerBusca(
    string? Cpf = null,
    string? Registro = null,
    string? Conselho = null,
    string? ConselhoDiferenteDe = null,
    string? Nome = null);

/// <summary>Operações sobre o recurso FHIR <c>Practitioner</c> (profissional de saúde).</summary>
public interface IPractitionerService
{
    Task<Practitioner> CriarAsync(Practitioner practitioner, CancellationToken ct = default);
    Task<Practitioner> LerAsync(Guid id, CancellationToken ct = default);
    Task<Practitioner> AtualizarAsync(Guid id, Practitioner practitioner, CancellationToken ct = default);
    Task ExcluirAsync(Guid id, CancellationToken ct = default);
    Task<Bundle> BuscarAsync(PractitionerBusca filtro, CancellationToken ct = default);
}
