namespace SMSMarica.Core.LaudoTemplates.Dtos;

public sealed record LaudoTemplateDto(
    Guid Id,
    string Nome,
    string Categoria,
    string? Descricao,
    string ConteudoJson,
    string ConteudoHtml,
    Guid CriadoPorUsuarioId,
    string? CriadoPorNome,
    DateTime CriadoEm,
    Guid? AtualizadoPorUsuarioId,
    string? AtualizadoPorNome,
    DateTime? AtualizadoEm,
    bool Ativo);

public sealed record LaudoTemplateListItemDto(
    Guid Id,
    string Nome,
    string Categoria,
    string? Descricao,
    DateTime CriadoEm,
    bool Ativo);
