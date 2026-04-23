namespace SMSMarica.Core.Motoristas.Dtos;

public sealed record AtualizarMotoristaRequest(
    string NomeCompleto,
    string Cnh,
    string? Telefone);
