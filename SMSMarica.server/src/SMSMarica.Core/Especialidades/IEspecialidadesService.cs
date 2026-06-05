using SMSMarica.Core.Especialidades.Dtos;

namespace SMSMarica.Core.Especialidades;

public interface IEspecialidadesService
{
    Task<IReadOnlyList<EspecialidadeListItemDto>> ListarAsync(
        bool incluirInativas, CancellationToken cancellationToken = default);

    Task<EspecialidadeDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarEspecialidadeRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarEspecialidadeRequest request, CancellationToken cancellationToken = default);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);
}
