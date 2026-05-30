using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

internal static class IdentidadeMapper
{
    public static UsuarioDto ParaDto(Usuario u, IEnumerable<Guid>? perfilIds = null) => new(
        u.Id, u.NomeCompleto, u.Email, u.Cpf, u.DataNascimento, u.Telefone,
        u.Endereco is null ? null : EnderecoDto.ParaDto(u.Endereco),
        u.FotoBase64,
        u.Ativo, u.CriadoEm, u.UltimoAcessoEm,
        [.. perfilIds ?? u.UsuariosPerfis.Select(up => up.PerfilId)],
        u.DeveTrocarSenha,
        DetectarPapel(u));

    public static UsuarioListItemDto ParaListItem(Usuario u) =>
        new(u.Id, u.NomeCompleto, u.Email, u.FotoBase64, u.Ativo, u.DeveTrocarSenha);

    private static string? DetectarPapel(Usuario u)
    {
        if (u.Medico is not null) return "Medico";
        if (u.Motorista is not null) return "Motorista";
        if (u.Paciente is not null) return "Paciente";
        return null;
    }
}
