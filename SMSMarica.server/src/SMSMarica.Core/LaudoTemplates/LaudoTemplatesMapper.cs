using SMSMarica.Core.LaudoTemplates.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.LaudoTemplates;

internal static class LaudoTemplatesMapper
{
    public static LaudoTemplateDto ParaDto(LaudoTemplate t) => new(
        t.Id,
        t.Nome,
        t.Categoria,
        t.Descricao,
        t.ConteudoJson,
        t.ConteudoHtml,
        t.EstruturaJson,
        t.CriadoPorUsuarioId,
        t.CriadoPorUsuario?.NomeCompleto,
        t.CriadoEm,
        t.AtualizadoPorUsuarioId,
        t.AtualizadoPorUsuario?.NomeCompleto,
        t.AtualizadoEm,
        t.Ativo);

    public static LaudoTemplateListItemDto ParaListItem(LaudoTemplate t) => new(
        t.Id,
        t.Nome,
        t.Categoria,
        t.Descricao,
        !string.IsNullOrWhiteSpace(t.EstruturaJson),
        t.CriadoEm,
        t.Ativo);
}
