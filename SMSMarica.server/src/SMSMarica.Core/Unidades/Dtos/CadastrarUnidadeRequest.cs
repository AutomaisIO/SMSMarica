namespace SMSMarica.Core.Unidades.Dtos;

public sealed record CadastrarUnidadeRequest(
    string Nome,
    string Endereco,
    string? Telefone,
    double Latitude,
    double Longitude);
