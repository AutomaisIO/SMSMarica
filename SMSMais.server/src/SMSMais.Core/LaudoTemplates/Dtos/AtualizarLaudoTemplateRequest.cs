namespace SMSMais.Core.LaudoTemplates.Dtos;

public sealed record AtualizarLaudoTemplateRequest(
    string Nome,
    string Categoria,
    string? Descricao,
    string ConteudoJson,
    string ConteudoHtml,
    string? EstruturaJson = null);
