using SMSMarica.Core.Pacientes.Agendamentos.Dtos;

namespace SMSMarica.Core.Pacientes.Agendamentos;

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
}
