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
    /// <summary>true = só com paciente vinculado; false = só "Não vinculado"; null = todos.</summary>
    bool? Vinculado = null,
    /// <summary>true = só assinados (assinatura concluída); false = só não assinados; null = todos.</summary>
    bool? Assinado = null,
    int Limite = 50);
