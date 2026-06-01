using SMSMarica.Core.Medicos.Dtos;

namespace SMSMarica.Core.Medicos;

public interface IMedicosService
{
    /// <summary>
    /// Busca profissionais no hub FHIR. <paramref name="conselho"/> filtra por sigla
    /// exata (ex.: "CRM" para o menu Médicos); <paramref name="conselhoExceto"/> exclui
    /// uma sigla (ex.: "CRM" para o menu Profissionais = todos menos médicos).
    /// </summary>
    Task<IReadOnlyList<MedicoListItemDto>> BuscarAsync(
        string? termo,
        string? conselho = null,
        string? conselhoExceto = null,
        CancellationToken cancellationToken = default);
    Task<MedicoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarMedicoRequest request, CancellationToken cancellationToken = default);
    Task<Guid> PromoverAsync(PromoverMedicoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarMedicoRequest request, CancellationToken cancellationToken = default);
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}
