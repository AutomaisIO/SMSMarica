using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Dtos;

public sealed record SessaoElegivelDto(
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    StatusSessao Status,
    bool Vencida,
    /// <summary>Condição do paciente registrada no atendimento — para quem monta a rota.</summary>
    NecessidadesDto Necessidades,
    /// <summary>Acompanhantes previstos na viagem (escolhidos, ou 1 se só foi confirmado que vem alguém).</summary>
    int AcompanhantesPrevistos,
    /// <summary>Limite do atendimento: 1, ou 2 com liberação.</summary>
    int LimiteAcompanhantes);
