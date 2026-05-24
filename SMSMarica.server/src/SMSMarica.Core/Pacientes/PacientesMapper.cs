using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Mapeamento Paciente ↔ DTOs. Manual (sem Mapperly) porque envolve
/// owned entities nullable, enums e listas mutáveis. Após ADR-0005,
/// dados pessoais base vêm de <see cref="Paciente.Usuario"/>.
/// </summary>
internal static class PacientesMapper
{
    public static PacienteDto ParaDto(Paciente p) => new(
        p.Id,
        p.Usuario.NomeCompleto,
        p.Usuario.Cpf ?? string.Empty,
        p.Cns,
        p.GpsResidencia?.Latitude ?? 0,
        p.GpsResidencia?.Longitude ?? 0,
        p.ExcluidoEm == null,
        p.CriadoEm,
        p.Usuario.Rg,
        p.Usuario.DataNascimento,
        p.Usuario.Sexo ?? Sexo.NaoInformado,
        p.EstadoCivil,
        p.RacaCor,
        p.Escolaridade,
        p.Ocupacao,
        p.Naturalidade,
        p.Nacionalidade,
        p.NomeDaMae,
        p.NomeDoPai,
        p.ResponsavelLegal,
        p.Usuario.Endereco is null ? null : EnderecoDto.ParaDto(p.Usuario.Endereco),
        p.Usuario.Telefone,
        p.TelefoneCelular,
        p.TelefoneResidencial,
        p.Usuario.Email,
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
        p.Observacoes,
        p.Usuario.FotoBase64,
        p.NomeSocial);

    public static PacienteListItemDto ParaListItem(Paciente p) => new(
        p.Id,
        p.Usuario.NomeCompleto,
        p.Usuario.Cpf ?? string.Empty,
        p.Usuario.DataNascimento,
        p.NomeDaMae,
        p.Usuario.Telefone,
        p.Usuario.FotoBase64,
        p.ExcluidoEm == null,
        p.NomeSocial);

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
