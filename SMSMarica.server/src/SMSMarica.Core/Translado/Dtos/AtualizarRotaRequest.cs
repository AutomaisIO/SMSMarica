namespace SMSMarica.Core.Translado.Dtos;

public sealed record AtualizarRotaRequest(
    Guid VeiculoId,
    Guid MotoristaId);
