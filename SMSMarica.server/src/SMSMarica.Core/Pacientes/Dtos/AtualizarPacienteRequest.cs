namespace SMSMarica.Core.Pacientes.Dtos;

public sealed record AtualizarPacienteRequest(
    string NomeCompleto,
    string? Cns,
    double Latitude,
    double Longitude);
