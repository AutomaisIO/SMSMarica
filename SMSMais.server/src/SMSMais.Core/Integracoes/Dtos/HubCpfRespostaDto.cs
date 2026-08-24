namespace SMSMais.Core.Integracoes.Dtos;

/// <summary>Resposta normalizada da consulta CPF (Hub do Desenvolvedor v2).</summary>
public sealed record HubCpfRespostaDto(
    string Cpf,
    string Nome,
    string DataNascimento,
    string? SituacaoCadastral,
    /// <summary>Sexo normalizado ("Masculino"/"Feminino") quando a Receita/Hub retorna; null caso contrário.</summary>
    string? Sexo = null);

/// <summary>Resposta normalizada da consulta CEP (Hub do Desenvolvedor v2 — cep3).</summary>
public sealed record HubCepRespostaDto(
    string Cep,
    string Logradouro,
    string? Complemento,
    string Bairro,
    string Localidade,
    string Uf,
    string? Ibge);
