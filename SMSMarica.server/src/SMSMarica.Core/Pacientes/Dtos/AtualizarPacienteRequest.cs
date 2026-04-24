using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes.Dtos;

public sealed record AtualizarPacienteRequest(
    string NomeCompleto,
    string? Cns,
    string? Rg,
    Sexo Sexo = Sexo.NaoInformado,
    EstadoCivil EstadoCivil = EstadoCivil.NaoInformado,
    RacaCor RacaCor = RacaCor.NaoInformado,
    Escolaridade Escolaridade = Escolaridade.NaoInformado,
    string? Ocupacao = null,
    string? Naturalidade = null,
    string? Nacionalidade = "Brasileira",
    string? NomeDaMae = null,
    string? NomeDoPai = null,
    string? ResponsavelLegal = null,
    EnderecoDto? Endereco = null,
    string? TelefonePrincipal = null,
    string? TelefoneCelular = null,
    string? TelefoneResidencial = null,
    string? Email = null,
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
