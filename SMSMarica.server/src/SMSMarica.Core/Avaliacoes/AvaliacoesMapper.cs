using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Avaliacoes.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Avaliacoes;

[Mapper]
internal static partial class AvaliacoesMapper
{
    public static AvaliacaoDto ParaDto(Avaliacao a) =>
        new(a.Id, a.SessaoId, a.Nota, a.Comentario, a.CriadoEm);

    public static AvaliacaoListItemDto ParaListItem(Avaliacao a) =>
        new(a.Id, a.SessaoId, a.Nota);
}
