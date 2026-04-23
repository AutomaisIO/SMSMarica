namespace SMSMarica.Core.Veiculos.Dtos;

public sealed record AtualizarVeiculoRequest(
    string Placa,
    string Modelo);
