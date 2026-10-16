using SMSMais.Core.Avaliacoes.Dtos;

namespace SMSMais.Core.Avaliacoes;

public interface IAvaliacoesService
{
    Task<IReadOnlyList<AvaliacaoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<AvaliacaoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> RegistrarAsync(RegistrarAvaliacaoRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Guid id, AtualizarAvaliacaoRequest request, CancellationToken cancellationToken = default);
    Task DeletarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>App do cidadão: as viagens feitas nos últimos <see cref="AvaliacoesService.DiasParaAvaliar"/>
    /// dias, com a nota já dada (se houver).</summary>
    Task<IReadOnlyList<ViagemParaAvaliarDto>> ViagensParaAvaliarAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>App do cidadão: o próprio paciente avalia uma viagem feita (uma vez por viagem, dentro
    /// do prazo). Fica com o motorista, o veículo e a rota daquela viagem.</summary>
    Task<Guid> AvaliarPeloPacienteAsync(
        Guid pacienteId, Guid sessaoId, AvaliarViagemRequest request, CancellationToken cancellationToken = default);
}
