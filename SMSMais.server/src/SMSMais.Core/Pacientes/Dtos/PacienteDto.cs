using SMSMais.Core.Common.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Dtos;

/// <summary>
/// Detalhe completo de um paciente. Shape mantém os campos originais
/// consumidos por SMSMais.cidadao.app (nomeCompleto, cpf, cns,
/// latitude, longitude, ativo, cadastradoEm). Campos novos adicionados
/// ao final são ignorados silenciosamente pelo cliente antigo.
/// </summary>
public sealed record PacienteDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    string? Cns,
    double Latitude,
    double Longitude,
    bool Ativo,
    DateTime CadastradoEm,
    // Identificação
    string? Rg,
    DateOnly? DataNascimento,
    Sexo Sexo,
    EstadoCivil EstadoCivil,
    RacaCor RacaCor,
    Escolaridade Escolaridade,
    string? Ocupacao,
    string? Naturalidade,
    string Nacionalidade,
    // Filiação
    string? NomeDaMae,
    string? NomeDoPai,
    string? ResponsavelLegal,
    // Endereço
    EnderecoDto? Endereco,
    // Contatos
    string? TelefonePrincipal,
    string? TelefoneCelular,
    string? TelefoneResidencial,
    string? Email,
    ContatoEmergenciaDto? ContatoEmergencia,
    // Saúde
    int? AlturaCm,
    decimal? PesoKg,
    TipoSanguineo TipoSanguineo,
    FatorRh FatorRh,
    IReadOnlyList<string> Alergias,
    IReadOnlyList<string> MedicamentosContinuos,
    IReadOnlyList<string> Comorbidades,
    IReadOnlyList<string> Deficiencias,
    string? PlanoSaude,
    // Outros
    string? Observacoes,
    string? FotoBase64,
    // Adicionado depois — cidadao.app ignora silenciosamente.
    string? NomeSocial = null,
    // --- Tudo que vem do recurso FHIR (campos extras do hub) ---
    /// <summary>Todos os identificadores do recurso FHIR (CPF, CNS, RG, PIS,
    /// passaporte, RNE, certidão, prontuários SGH/CEM, cd_paciente Salux...).</summary>
    IReadOnlyList<IdentificadorDto>? Identificadores = null,
    /// <summary>Data de óbito (FHIR Patient.deceasedDateTime), se houver.</summary>
    DateOnly? DataObito = null,
    string? NomeConjuge = null,
    /// <summary>Sistema de origem do recurso no hub (FHIR Meta.source).</summary>
    string? Fonte = null,
    /// <summary>Dados crus da fonte preservados em extension (ex.: códigos Salux:
    /// cor, nacionalidade, religião, etnia, peso/altura, sangue/RH).</summary>
    IReadOnlyDictionary<string, string>? DadosFonte = null,
    /// <summary>Número do contato VERIFICADO por OTP (marcador no telecom FHIR), se houver.
    /// Fonte única do "telefone verificado" — é por ele que a SMS fala com a pessoa.</summary>
    string? TelefoneVerificado = null,
    DateTime? TelefoneVerificadoEm = null,
    /// <summary>A que título o número verificado atende este paciente: "Proprio",
    /// "MaeOuPaiOuResponsavel" ou "OutroParenteOuCuidador". Só faz sentido com
    /// <see cref="TelefoneVerificado"/> preenchido.</summary>
    string? TelefoneVerificadoVinculo = null,
    /// <summary>Número NEGADO ("não sou essa pessoa" — marcador no telecom FHIR), se houver.
    /// É o alerta ❗: mensagens para este número chegam à pessoa errada.</summary>
    string? TelefoneNegado = null,
    DateTime? TelefoneNegadoEm = null);

/// <summary>Identificador FHIR (system + valor) — ex.: CPF, CNS, RG, prontuário.</summary>
public sealed record IdentificadorDto(string Sistema, string Valor);

public sealed record PacienteListItemDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string? NomeDaMae,
    string? TelefonePrincipal,
    string? FotoBase64,
    bool Ativo,
    string? NomeSocial = null,
    /// <summary>
    /// Paciente sem CPF, vindo de um PEP — não é possível uni-lo ao mesmo cidadão em outra
    /// base, então ele PODE aparecer repetido. A tela mostra o selo ao lado do nome para que
    /// quem atende saiba que aquela identidade não está confirmada.
    /// </summary>
    bool IdentidadeIncompleta = false);

public sealed record ContatoEmergenciaDto(
    string Nome,
    string? Parentesco,
    string Telefone);
