namespace SMSMarica.Core.Unidades.Dtos;

public sealed record AtualizarUnidadeRequest(
    string Nome,
    string Endereco,
    string? Telefone,
    double Latitude,
    double Longitude);
