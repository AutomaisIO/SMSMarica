using SMSMarica.Core.TiposExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame;

public interface ITiposExameService
{
    Task<IReadOnlyList<TipoExameListItemDto>> ListarAsync(
        ModalidadeDicom? modalidade,
        bool incluirInativos,
        CancellationToken cancellationToken = default);

    Task<TipoExameDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarTipoExameRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarTipoExameRequest request, CancellationToken cancellationToken = default);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);
}
