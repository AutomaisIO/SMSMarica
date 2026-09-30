using SMSMais.Core.Pacientes.Agendamentos.Dtos;

namespace SMSMais.Core.Pacientes.Agendamentos;

/// <summary>
/// Agrega, EM LEITURA, os agendamentos de um paciente vindos das três fontes já materializadas
/// localmente (SER — <c>ser_solicitacao</c>; SISREG — <c>solicitacao</c>; agenda própria —
/// <c>agendamento</c>). Não persiste nada: cada fonte segue como sua própria varredura a mantém.
/// </summary>
public interface IAgendamentosPacienteService
{
    /// <summary>
    /// Lista os agendamentos do paciente, separados em próximos e histórico, com situação
    /// normalizada entre as fontes.
    /// </summary>
    Task<AgendamentosPacienteDto> ListarPorPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// O que as saídas PARA O PACIENTE (robô do WhatsApp e app do cidadão) mostram da regulação
    /// externa — SER, SERNIT e ESUS de São Gonçalo: os pedidos <b>na fila</b> (em fila ou
    /// pendentes, sem distinção) e os <b>agendados com data futura</b>.
    ///
    /// <para><b>Regra do Bernardo (30/09/2026):</b> na fila é "está na fila" e nada mais — nunca o
    /// motivo da pendência, a posição, a prioridade ou previsão. Por isso Pendente sai como
    /// <see cref="SituacaoAgendamentoPaciente.EmFila"/>. Agendado sem data legível fica de fora:
    /// para o paciente só se afirma agendamento com data.</para>
    /// </summary>
    Task<IReadOnlyList<AgendamentoPacienteItemDto>> RegulacaoParaPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default);
}
