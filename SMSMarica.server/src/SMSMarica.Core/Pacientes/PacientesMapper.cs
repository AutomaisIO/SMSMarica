using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Mapeamento Paciente ↔ DTOs. Manual (sem Mapperly) porque envolve
/// owned entities nullable, enums e listas mutáveis.
/// </summary>
internal static class PacientesMapper
{
    public static PacienteDto ParaDto(Paciente p) => new(
        p.Id,
        p.NomeCompleto,
        p.Cpf,
        p.Cns,
        p.GpsResidencia?.Latitude ?? 0,
        p.GpsResidencia?.Longitude ?? 0,
        p.Ativo,
        p.CriadoEm,
        p.Rg,
        p.DataNascimento,
        p.Sexo,
        p.EstadoCivil,
        p.RacaCor,
        p.Escolaridade,
        p.Ocupacao,
        p.Naturalidade,
        p.Nacionalidade,
        p.NomeDaMae,
        p.NomeDoPai,
        p.ResponsavelLegal,
        p.Endereco is null ? null : EnderecoDto.ParaDto(p.Endereco),
        p.TelefonePrincipal,
        p.TelefoneCelular,
        p.TelefoneResidencial,
        p.Email,
        p.ContatoEmergencia is null ? null : ParaContatoDto(p.ContatoEmergencia),
        p.AlturaCm,
        p.PesoKg,
        p.TipoSanguineo,
        p.FatorRh,
        p.Alergias,
        p.MedicamentosContinuos,
        p.Comorbidades,
        p.Deficiencias,
        p.PlanoSaude,
        p.Observacoes);

    public static PacienteListItemDto ParaListItem(Paciente p) => new(
        p.Id,
        p.NomeCompleto,
        p.Cpf,
        p.DataNascimento,
        p.NomeDaMae,
        p.TelefonePrincipal,
        p.Ativo);

    public static ContatoEmergenciaDto ParaContatoDto(ContatoEmergencia c) => new(
        c.Nome,
        c.Parentesco,
        c.Telefone);

    public static ContatoEmergencia ParaEntidade(ContatoEmergenciaDto dto) => new()
    {
        Nome = dto.Nome ?? string.Empty,
        Parentesco = string.IsNullOrWhiteSpace(dto.Parentesco) ? null : dto.Parentesco,
        Telefone = dto.Telefone ?? string.Empty,
    };
}
