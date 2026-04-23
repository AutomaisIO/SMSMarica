using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Pacientes;

[Mapper]
internal static partial class PacientesMapper
{
    public static PacienteDto ParaDto(Paciente paciente) => new(
        paciente.Id,
        paciente.NomeCompleto,
        paciente.Cpf,
        paciente.Cns,
        paciente.GpsResidencia.Latitude,
        paciente.GpsResidencia.Longitude,
        paciente.Ativo,
        paciente.CriadoEm);

    public static PacienteListItemDto ParaListItem(Paciente paciente) => new(
        paciente.Id,
        paciente.NomeCompleto,
        paciente.Cpf,
        paciente.Ativo);
}
