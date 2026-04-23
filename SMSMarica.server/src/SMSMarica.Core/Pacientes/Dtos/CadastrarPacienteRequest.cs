namespace SMSMarica.Core.Pacientes.Dtos;

public sealed record CadastrarPacienteRequest(
    string NomeCompleto,
    string Cpf,
    string? Cns,
    double Latitude,
    double Longitude);
