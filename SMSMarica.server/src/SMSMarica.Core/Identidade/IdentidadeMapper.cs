using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

[Mapper]
internal static partial class IdentidadeMapper
{
    public static UsuarioDto ParaDto(Usuario u) => new(
        u.Id, u.NomeCompleto, u.Email, u.Cpf, u.Perfil, u.Ativo, u.CriadoEm, u.UltimoAcessoEm);

    public static UsuarioListItemDto ParaListItem(Usuario u) =>
        new(u.Id, u.NomeCompleto, u.Email, u.Perfil, u.Ativo);
}
