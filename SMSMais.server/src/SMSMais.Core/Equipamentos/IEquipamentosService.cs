using SMSMais.Core.Equipamentos.Dtos;

namespace SMSMais.Core.Equipamentos;

public interface IEquipamentosService
{
    Task<IReadOnlyList<EquipamentoListItemDto>> ListarAsync(
        Guid? unidadeId, bool incluirInativos, CancellationToken cancellationToken = default);

    Task<EquipamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarEquipamentoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarEquipamentoRequest request, CancellationToken cancellationToken = default);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);
}
