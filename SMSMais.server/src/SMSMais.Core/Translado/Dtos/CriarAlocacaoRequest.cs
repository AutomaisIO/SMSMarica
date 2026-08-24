using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Dtos;

public sealed record CriarAlocacaoRequest(
    Guid SessaoId,
    Guid AssentoId,
    TipoAlocacao Tipo = TipoAlocacao.Paciente);
