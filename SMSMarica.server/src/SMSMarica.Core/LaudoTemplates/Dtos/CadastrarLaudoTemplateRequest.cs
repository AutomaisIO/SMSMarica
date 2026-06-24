namespace SMSMarica.Core.LaudoTemplates.Dtos;

public sealed record CadastrarLaudoTemplateRequest(
    string Nome,
    string Categoria,
    string? Descricao,
    string ConteudoJson,
    string ConteudoHtml,
    string? EstruturaJson = null);
