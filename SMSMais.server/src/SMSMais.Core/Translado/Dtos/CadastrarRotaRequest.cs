namespace SMSMais.Core.Translado.Dtos;

public sealed record CadastrarRotaRequest(
    DateOnly Data,
    Guid VeiculoId,
    Guid MotoristaId);
