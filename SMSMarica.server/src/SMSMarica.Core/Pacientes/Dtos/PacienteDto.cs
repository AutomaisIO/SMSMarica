namespace SMSMarica.Core.Pacientes.Dtos;

/// <summary>
/// Detalhe de um paciente. Shape compatível com SMSMarica.cidadao.app
/// (lib/features/perfil/data/perfil_repository.dart).
/// </summary>
public sealed record PacienteDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    string? Cns,
    double Latitude,
    double Longitude,
    bool Ativo,
    DateTime CadastradoEm);

public sealed record PacienteListItemDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    bool Ativo);
