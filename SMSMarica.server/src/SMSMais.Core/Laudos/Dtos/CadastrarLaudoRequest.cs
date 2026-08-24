namespace SMSMais.Core.Laudos.Dtos;

/// <summary>
/// Cria rascunho de laudo para um estudo PACS. O médico (papel Medico de
/// Usuario) é resolvido a partir do JWT.
/// </summary>
public sealed record CadastrarLaudoRequest(
    string StudyInstanceUID,
    Guid? PacienteId,
    Guid? LaudoTemplateId,
    string Titulo,
    string ConteudoJson,
    string ConteudoHtml,
    ChecklistLaudoInput? Checklist = null);
