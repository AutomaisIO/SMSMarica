namespace SMSMais.Core.Translado.Dtos;

public sealed record AtualizarRotaRequest(
    DateOnly Data,
    Guid VeiculoId,
    Guid MotoristaId);
