using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Translado.Dtos;

public sealed record CriarAlocacaoRequest(
    Guid SessaoId,
    Guid AssentoId,
    TipoAlocacao Tipo = TipoAlocacao.Paciente);
