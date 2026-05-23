using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

internal static class MotoristasMapper
{
    public static MotoristaDto ParaDto(Motorista m) => new(
        m.Id,
        m.UsuarioId,
        m.Usuario.NomeCompleto,
        m.Usuario.Cpf ?? string.Empty,
        m.Cnh,
        m.Usuario.Telefone,
        m.Usuario.Endereco is null ? null : EnderecoDto.ParaDto(m.Usuario.Endereco),
        m.Usuario.FotoBase64,
        m.Ativo,
        m.CriadoEm);

    public static MotoristaListItemDto ParaListItem(Motorista m) => new(
        m.Id,
        m.UsuarioId,
        m.Usuario.NomeCompleto,
        m.Usuario.Cpf ?? string.Empty,
        m.Usuario.FotoBase64,
        m.Ativo);
}
