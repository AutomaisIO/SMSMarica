using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

internal static class MotoristasMapper
{
    public static MotoristaDto ParaDto(Motorista m) => new(
        m.Id, m.NomeCompleto, m.Cpf, m.Cnh, m.Telefone,
        m.Endereco is null ? null : EnderecoDto.ParaDto(m.Endereco),
        m.FotoBase64,
        m.Ativo, m.CriadoEm);

    public static MotoristaListItemDto ParaListItem(Motorista m) =>
        new(m.Id, m.NomeCompleto, m.Cpf, m.FotoBase64, m.Ativo);
}
