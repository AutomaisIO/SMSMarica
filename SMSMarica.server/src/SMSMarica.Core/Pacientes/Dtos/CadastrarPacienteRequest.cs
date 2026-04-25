using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes.Dtos;

public sealed record CadastrarPacienteRequest(
    // Identificação (CPF e DataNascimento vêm da consulta Hub no passo 1)
    string NomeCompleto,
    string Cpf,
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
    string? Observacoes = null);
