using SMSMarica.Core.Tratamentos.Dtos;

namespace SMSMarica.Core.Tratamentos;

public interface ITratamentosService
{
    Task<IReadOnlyList<TratamentoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista tratamentos de um paciente (todos, ativos e encerrados).</summary>
    Task<IReadOnlyList<TratamentoListItemDto>> ListarPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoTratamentoDto>> ListarTiposAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Expande uma regra de periodicidade em uma lista de datas (preview antes
    /// de persistir). Para Manual retorna vazio — o front envia datas próprias.
    /// </summary>
    IReadOnlyList<DateOnly> ExpandirPeriodicidade(ExpandirPeriodicidadeRequest request);

    Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default);

    Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default);

    // --- Sessões

    Task<Guid> AdicionarSessaoAsync(Guid tratamentoId, AdicionarSessaoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarSessaoAsync(Guid tratamentoId, Guid sessaoId, AtualizarSessaoRequest request, CancellationToken cancellationToken = default);

    Task CancelarSessaoAsync(Guid tratamentoId, Guid sessaoId, CancellationToken cancellationToken = default);

    Task ConfirmarSessaoAsync(Guid tratamentoId, Guid sessaoId, ConfirmarSessaoRequest request, CancellationToken cancellationToken = default);
}
