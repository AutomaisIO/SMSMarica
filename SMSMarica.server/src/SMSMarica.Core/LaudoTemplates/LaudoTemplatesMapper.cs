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
        t.CriadoPorUsuarioId,
        t.CriadoPorUsuario?.NomeExibicao,
        t.CriadoEm,
        t.AtualizadoPorUsuarioId,
        t.AtualizadoPorUsuario?.NomeExibicao,
        t.AtualizadoEm,
        t.Ativo);

    public static LaudoTemplateListItemDto ParaListItem(LaudoTemplate t) => new(
        t.Id,
        t.Nome,
        t.Categoria,
        t.Descricao,
        t.CriadoEm,
        t.Ativo);
}
