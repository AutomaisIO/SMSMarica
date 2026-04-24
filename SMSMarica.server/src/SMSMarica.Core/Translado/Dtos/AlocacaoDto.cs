using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Translado.Dtos;

public sealed record AlocacaoDto(
    Guid Id,
    Guid SessaoId,
    Guid TratamentoId,
    Guid PacienteId,
    string PacienteNome,
    Guid UnidadeId,
    string UnidadeNome,
    TimeOnly? HoraPrevistaBusca,
    Guid AssentoId,
    int FileiraOrdem,
    int NumeroAssento,
    TipoAlocacao Tipo);
