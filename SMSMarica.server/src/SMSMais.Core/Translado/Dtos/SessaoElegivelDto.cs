using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Dtos;

public sealed record SessaoElegivelDto(
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    DateOnly DataPrevista,
    TimeOnly? HoraPrevistaBusca,
    StatusSessao Status,
    bool Vencida);
