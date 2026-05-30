using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

internal static class MotoristasMapper
{
    public static MotoristaDto ParaDto(Motorista m, Usuario? usuario) => new(
        m.Id,
        usuario?.Id ?? Guid.Empty,
        m.NomeCompleto,
        m.Cpf,
        m.DataNascimento,
        m.Cnh,
        m.Telefone,
        m.Endereco is null ? null : EnderecoDto.ParaDto(m.Endereco),
        m.FotoBase64,
        usuario?.Ativo ?? false,
        m.CriadoEm);

    public static MotoristaListItemDto ParaListItem(Motorista m, Usuario? usuario) => new(
        m.Id,
        usuario?.Id ?? Guid.Empty,
        m.NomeCompleto,
        m.Cpf,
        m.FotoBase64,
        usuario?.Ativo ?? false);
}
