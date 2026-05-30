using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Medicos;

internal static class MedicosMapper
{
    public static MedicoDto ParaDto(Medico m) => new(
        m.Id,
        m.UsuarioId,
        m.Usuario.NomeCompleto,
        m.Usuario.Cpf ?? string.Empty,
        m.Usuario.DataNascimento,
        m.Crm,
        m.UfCrm,
        m.Especialidade,
        m.Rqe,
        m.ValidadeCrm,
        m.Usuario.Telefone,
        m.Usuario.Endereco is null ? null : EnderecoDto.ParaDto(m.Usuario.Endereco),
        m.Usuario.FotoBase64,
        m.Usuario.Ativo,
        m.CriadoEm);

    public static MedicoListItemDto ParaListItem(Medico m) => new(
        m.Id,
        m.UsuarioId,
        m.Usuario.NomeCompleto,
        m.Usuario.Cpf ?? string.Empty,
        m.Crm,
        m.UfCrm,
        m.Especialidade,
        m.Usuario.FotoBase64,
        m.Usuario.Ativo);
}
