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
    // Nome cru do DICOM (0010,0010) capturado na criação — só rótulo temporário
    // enquanto não há vínculo (PacienteId). Limpo ao associar.
    string? PacienteNomeDicom,
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
    string? BiRads,
    string? BiRadsSugerido,
    string? RespostasChecklist,
    DateTime? FinalizadoEm,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    bool Assinado = false,
    // Elegibilidade da assinatura digital, resolvida no servidor (ObterPorId):
    // o usuário logado é o autor + laudo finalizado + não assinado + autor tem
    // rubrica. Deixa o front liberar/bloquear o botão "Assinar" sem precisar do
    // módulo Medicos (que o próprio médico não tem).
    bool PodeAssinar = false,
    string? MotivoBloqueioAssinatura = null,
    bool MedicoTemRubrica = false);

public sealed record LaudoListItemDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    Guid? PacienteId,
    string? PacienteNome,
    // Rótulo temporário do DICOM enquanto o exame não tem vínculo (ver LaudoDto).
    string? PacienteNomeDicom,
    Guid MedicoId,
    string MedicoNome,
    string Titulo,
    StatusLaudo Status,
    string? BiRads,
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
    StatusLaudo Status,
    bool Assinado = false);
