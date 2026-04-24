using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes.Dtos;

/// <summary>
/// Detalhe completo de um paciente. Shape mantém os campos originais
/// consumidos por SMSMarica.cidadao.app (nomeCompleto, cpf, cns,
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
    string? Observacoes);

public sealed record PacienteListItemDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string? NomeDaMae,
    string? TelefonePrincipal,
    bool Ativo);

public sealed record EnderecoDto(
    string Cep,
    string Logradouro,
    string? Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Uf,
    string? PontoReferencia);

public sealed record ContatoEmergenciaDto(
    string Nome,
    string? Parentesco,
    string Telefone);
