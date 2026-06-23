namespace SMSMarica.Core.Laudos.Dtos;

/// <summary>Atualiza um rascunho. Falha (409) se o laudo já estiver finalizado.</summary>
public sealed record AtualizarLaudoRequest(
    Guid? PacienteId,
    string Titulo,
    string ConteudoJson,
    string ConteudoHtml,
    ChecklistLaudoInput? Checklist = null);
