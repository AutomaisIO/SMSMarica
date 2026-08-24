using SMSMais.Core.Common.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Dtos;

public sealed record CadastrarPacienteRequest(
    // Identificação (CPF e DataNascimento vêm da consulta Hub no passo 1)
    string NomeCompleto,
    /// <summary>CPF com DV válido, ou <c>null</c> quando a origem não tem — o paciente entra
    /// ancorado no CNS e a recepção informa o CPF depois (ADR-0041 / ADR-0009).</summary>
    string? Cpf,
    DateOnly DataNascimento,
    string? Cns,
    string? Rg,
    Sexo Sexo = Sexo.NaoInformado,
    EstadoCivil EstadoCivil = EstadoCivil.NaoInformado,
    RacaCor RacaCor = RacaCor.NaoInformado,
    Escolaridade Escolaridade = Escolaridade.NaoInformado,
    string? Ocupacao = null,
    string? Naturalidade = null,
    string? Nacionalidade = "Brasileira",
    // Filiação
    string? NomeDaMae = null,
    string? NomeDoPai = null,
    string? ResponsavelLegal = null,
    // Endereço
    EnderecoDto? Endereco = null,
    // Contatos
    string? TelefonePrincipal = null,
    string? TelefoneCelular = null,
    string? TelefoneResidencial = null,
    string? Email = null,
    ContatoEmergenciaDto? ContatoEmergencia = null,
    // Saúde
    int? AlturaCm = null,
    decimal? PesoKg = null,
    TipoSanguineo TipoSanguineo = TipoSanguineo.NaoInformado,
    FatorRh FatorRh = FatorRh.NaoInformado,
    IReadOnlyList<string>? Alergias = null,
    IReadOnlyList<string>? MedicamentosContinuos = null,
    IReadOnlyList<string>? Comorbidades = null,
    IReadOnlyList<string>? Deficiencias = null,
    string? PlanoSaude = null,
    // Outros
    string? Observacoes = null,
    string? FotoBase64 = null,
    /// <summary>Nome pelo qual o paciente prefere ser chamado.</summary>
    string? NomeSocial = null);

/// <summary>
/// Promove um Usuario existente (sem papel) a Paciente. Carrega apenas os
/// campos específicos de paciente — dados pessoais base reutilizam o que já
/// está em <c>usuario</c>.
/// </summary>
public sealed record PromoverPacienteRequest(
    Guid UsuarioId,
    string? Cns = null,
    string? NomeSocial = null,
    EstadoCivil EstadoCivil = EstadoCivil.NaoInformado,
    RacaCor RacaCor = RacaCor.NaoInformado,
    Escolaridade Escolaridade = Escolaridade.NaoInformado,
    string? Ocupacao = null,
    string? Naturalidade = null,
    string? Nacionalidade = "Brasileira",
    string? NomeDaMae = null,
    string? NomeDoPai = null,
    string? ResponsavelLegal = null,
    string? TelefoneCelular = null,
    string? TelefoneResidencial = null,
    ContatoEmergenciaDto? ContatoEmergencia = null,
    int? AlturaCm = null,
    decimal? PesoKg = null,
    TipoSanguineo TipoSanguineo = TipoSanguineo.NaoInformado,
    FatorRh FatorRh = FatorRh.NaoInformado,
    IReadOnlyList<string>? Alergias = null,
    IReadOnlyList<string>? MedicamentosContinuos = null,
    IReadOnlyList<string>? Comorbidades = null,
    IReadOnlyList<string>? Deficiencias = null,
    string? PlanoSaude = null,
    string? Observacoes = null);
