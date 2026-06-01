using SMSMarica.Core.Atendimentos.Dtos;

namespace SMSMarica.Core.Atendimentos;

public interface IAtendimentosService
{
    /// <summary>Timeline de atendimentos (Encounter + Condition) de um paciente, do hub FHIR.</summary>
    Task<IReadOnlyList<AtendimentoDto>> ObterPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default);
}
