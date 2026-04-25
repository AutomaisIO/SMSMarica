using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

internal static class IdentidadeMapper
{
    public static UsuarioDto ParaDto(Usuario u) => new(
        u.Id, u.NomeCompleto, u.Email, u.Cpf, u.Telefone,
        u.Endereco is null ? null : EnderecoDto.ParaDto(u.Endereco),
        u.FotoBase64,
        u.Perfil, u.Ativo, u.CriadoEm, u.UltimoAcessoEm);

    public static UsuarioListItemDto ParaListItem(Usuario u) =>
        new(u.Id, u.NomeCompleto, u.Email, u.FotoBase64, u.Perfil, u.Ativo);
}
