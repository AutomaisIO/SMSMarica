using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes;

public interface IPacientesService
{
    Task<IReadOnlyList<PacienteListItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default);

    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);
}
