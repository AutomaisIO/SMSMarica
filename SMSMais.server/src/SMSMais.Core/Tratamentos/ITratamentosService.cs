using SMSMais.Core.Tratamentos.Dtos;

namespace SMSMais.Core.Tratamentos;

/// <summary>Atendimentos do Transporte de Pacientes (na tela: "Atendimentos"; no código, tratamento).</summary>
public interface ITratamentosService
{
    Task<IReadOnlyList<TratamentoListItemDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista atendimentos de um paciente (todos, ativos e encerrados).</summary>
    Task<IReadOnlyList<TratamentoListItemDto>> ListarPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>Lista atendimentos com destino numa unidade de atendimento (todos, ativos e encerrados).</summary>
    Task<IReadOnlyList<TratamentoListItemDto>> ListarPorUnidadeAtendimentoAsync(Guid unidadeAtendimentoId, CancellationToken cancellationToken = default);

    Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoTratamentoDto>> ListarTiposAsync(CancellationToken cancellationToken = default);

    /// <summary>As datas que a agenda geraria hoje (prévia do cadastro e da troca de agenda).</summary>
    PreviaAgendaDto PreverAgenda(AgendaRequest agenda);

    Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Troca a agenda a partir da data de início da agenda nova (hoje ou depois): sessões pendentes
    /// e não alocadas daquele dia em diante saem; as novas nascem pela regra nova, sem repetir dia
    /// que já tem sessão. Realizadas, confirmadas e alocadas ficam.
    /// </summary>
    Task AlterarAgendaAsync(Guid id, AgendaRequest agenda, CancellationToken cancellationToken = default);

    /// <summary>Encerra o atendimento e cancela as sessões futuras que ainda não estão numa rota.</summary>
    Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estende os atendimentos contínuos até o fim do mês seguinte. Idempotente; pula atendimento
    /// de paciente com óbito registrado. Devolve quantos atendimentos ganharam sessões.
    /// </summary>
    Task<int> RenovarContinuosAsync(DateOnly hoje, CancellationToken cancellationToken = default);

    /// <summary>
    /// Próximas viagens do paciente (hoje em diante, sem as canceladas) nos atendimentos ativos —
    /// o que o app do cidadão mostra. No máximo <paramref name="limite"/> itens.
    /// </summary>
    Task<IReadOnlyList<ViagemTransporteDto>> ListarProximasViagensAsync(Guid pacienteId, int limite = 30, CancellationToken cancellationToken = default);

    // --- Sessões

    Task<Guid> AdicionarSessaoAsync(Guid tratamentoId, AdicionarSessaoRequest request, CancellationToken cancellationToken = default);

    Task AtualizarSessaoAsync(Guid tratamentoId, Guid sessaoId, AtualizarSessaoRequest request, CancellationToken cancellationToken = default);

    Task CancelarSessaoAsync(Guid tratamentoId, Guid sessaoId, CancellationToken cancellationToken = default);

    Task ConfirmarSessaoAsync(Guid tratamentoId, Guid sessaoId, ConfirmarSessaoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Define quem vai acompanhar o paciente numa viagem (da lista dele, até o limite).</summary>
    Task DefinirAcompanhantesDaSessaoAsync(Guid tratamentoId, Guid sessaoId, DefinirAcompanhantesSessaoRequest request, CancellationToken cancellationToken = default);
}
