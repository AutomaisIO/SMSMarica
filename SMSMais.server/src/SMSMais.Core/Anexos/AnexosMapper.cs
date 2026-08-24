using Riok.Mapperly.Abstractions;
using SMSMais.Core.Anexos.Dtos;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Anexos;

[Mapper]
internal static partial class AnexosMapper
{
    public static AnexoExameDto ParaDto(DocumentoExame d) => new(
        d.Id,
        d.Nome,
        d.Descricao,
        d.MimeType,
        d.TamanhoBytes,
        d.Status,
        d.Origem,
        d.Paginas,
        d.CriadoEm,
        $"/anexos/{d.Id}/conteudo");
}
