using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Laudos.Dtos;

public sealed record FiltroLaudosDto(
    string? StudyInstanceUID = null,
    Guid? PacienteId = null,
    Guid? MedicoId = null,
    StatusLaudo? Status = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    string? BiRads = null,
    int Limite = 50);
