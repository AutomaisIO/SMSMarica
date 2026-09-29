using SMSMais.Core.UnidadesAtendimento.Dtos;

namespace SMSMais.Core.UnidadesAtendimento;

/// <summary>
/// Cadastro das unidades de atendimento — os destinos do Transporte de Pacientes. A coordenada
/// de cada uma é o ponto final da rota da van; por isso nenhuma entra (nem é editada) sem ela.
/// </summary>
public interface IUnidadesAtendimentoService
{
    Task<IReadOnlyList<UnidadeAtendimentoListItemDto>> ListarAsync(bool incluirInativas, CancellationToken cancellationToken = default);

    /// <summary>Unidades ativas, para o seletor de destino do tratamento.</summary>
    Task<IReadOnlyList<UnidadeAtendimentoOpcaoDto>> ListarOpcoesAsync(CancellationToken cancellationToken = default);

    Task<UnidadeAtendimentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(SalvarUnidadeAtendimentoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, SalvarUnidadeAtendimentoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Tira a unidade das opções de destino. Recusa enquanto houver tratamento ativo
    /// apontando para ela — senão a rota continuaria indo a um destino "desativado".</summary>
    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
