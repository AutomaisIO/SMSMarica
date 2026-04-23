using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

[Mapper]
internal static partial class MotoristasMapper
{
    public static MotoristaDto ParaDto(Motorista m) => new(
        m.Id, m.NomeCompleto, m.Cpf, m.Cnh, m.Telefone, m.Ativo, m.CriadoEm);

    public static MotoristaListItemDto ParaListItem(Motorista m) =>
        new(m.Id, m.NomeCompleto, m.Cpf, m.Ativo);
}
