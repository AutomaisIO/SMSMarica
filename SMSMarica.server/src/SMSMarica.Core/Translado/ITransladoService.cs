using SMSMarica.Core.Translado.Dtos;

namespace SMSMarica.Core.Translado;

public interface ITransladoService
{
    /// <summary>Lista rotas. Se <paramref name="data"/> for informado, filtra por ela.</summary>
    Task<IReadOnlyList<RotaDiariaListItemDto>> ListarAsync(DateOnly? data, CancellationToken cancellationToken = default);
    Task<RotaDiariaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarRotaRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarRotaRequest request, CancellationToken cancellationToken = default);
    Task IniciarAsync(Guid id, CancellationToken cancellationToken = default);
    Task ConcluirAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelarAsync(Guid id, CancellationToken cancellationToken = default);
}
