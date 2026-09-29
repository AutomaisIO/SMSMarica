using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Dtos;

public sealed record AlocacaoDto(
    Guid Id,
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeAtendimentoId,
    string UnidadeAtendimentoNome,
    TimeOnly? HoraPrevistaBusca,
    Guid AssentoId,
    int FileiraOrdem,
    int NumeroAssento,
    TipoAlocacao Tipo);
