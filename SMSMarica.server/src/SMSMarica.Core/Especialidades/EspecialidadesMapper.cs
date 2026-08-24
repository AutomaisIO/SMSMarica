using SMSMarica.Core.Especialidades.Dtos;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Especialidades;

internal static class EspecialidadesMapper
{
    public static EspecialidadeDto ParaDto(Especialidade e) => new(
        e.Id,
        e.Nome,
        e.CodigoCbo,
        e.Ativo,
        e.CriadoEm);

    public static EspecialidadeListItemDto ParaListItem(Especialidade e) => new(
        e.Id,
        e.Nome,
        e.CodigoCbo,
        e.Ativo);
}
