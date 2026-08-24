using SMSMais.Core.TiposExame.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.TiposExame;

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
