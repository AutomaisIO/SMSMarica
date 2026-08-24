using SMSMais.Core.Translado.Dtos;

namespace SMSMais.Core.Translado;

public interface ITransladoService
{
    /// <summary>
    /// Lista rotas. Filtros opcionais: <paramref name="data"/>, <paramref name="motoristaId"/>, <paramref name="veiculoId"/>.
    /// </summary>
    Task<IReadOnlyList<RotaDiariaListItemDto>> ListarAsync(
        DateOnly? data,
        Guid? motoristaId,
        Guid? veiculoId,
        CancellationToken cancellationToken = default);

    Task<RotaDiariaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CadastrarAsync(CadastrarRotaRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarRotaRequest request, CancellationToken cancellationToken = default);
    Task IniciarAsync(Guid id, CancellationToken cancellationToken = default);
    Task ConcluirAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista sessões elegíveis para alocação em uma rota: pendentes/confirmadas
    /// cuja data prevista é ≤ data da rota e que ainda não estão alocadas em
    /// nenhuma outra rota não-cancelada.
    /// </summary>
    Task<IReadOnlyList<SessaoElegivelDto>> ListarSessoesElegiveisAsync(
        Guid rotaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aloca uma sessão num assento do veículo da rota. Se a sessão for atrasada,
    /// remarca DataPrevista para a data da rota (sai da fila de pendências).
    /// </summary>
    Task<Guid> CriarAlocacaoAsync(
        Guid rotaId, CriarAlocacaoRequest request, CancellationToken cancellationToken = default);

    Task RemoverAlocacaoAsync(
        Guid rotaId, Guid alocacaoId, CancellationToken cancellationToken = default);
}
