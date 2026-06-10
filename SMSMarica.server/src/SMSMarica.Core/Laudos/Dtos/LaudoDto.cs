using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Laudos.Dtos;

public sealed record LaudoDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    Guid? LaudoAnteriorId,
    Guid? PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    Guid MedicoId,
    string MedicoNome,
    string MedicoCrm,
    string MedicoUfCrm,
    string? MedicoRqe,
    Guid? LaudoTemplateId,
    string? LaudoTemplateNome,
    string Titulo,
    string ConteudoJson,
    string ConteudoHtml,
    StatusLaudo Status,
    DateTime? FinalizadoEm,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    bool Assinado = false);

public sealed record LaudoListItemDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    Guid? PacienteId,
    string? PacienteNome,
    Guid MedicoId,
    string MedicoNome,
    string Titulo,
    StatusLaudo Status,
    DateTime? FinalizadoEm,
    DateTime CriadoEm,
    bool Assinado = false);

public sealed record LaudoHistoricoItemDto(
    Guid Id,
    int Versao,
    StatusLaudo Status,
    Guid MedicoId,
    string MedicoNome,
    DateTime CriadoEm,
    DateTime? FinalizadoEm);

/// <summary>
/// Linha do dicionário StudyInstanceUID → laudo. Usado pela listagem PACS
/// para saber se já existe laudo (último, ativo) por exame em uma chamada só.
/// </summary>
public sealed record LaudoPorStudyDto(
    string StudyInstanceUID,
    Guid LaudoId,
    int Versao,
    StatusLaudo Status);
