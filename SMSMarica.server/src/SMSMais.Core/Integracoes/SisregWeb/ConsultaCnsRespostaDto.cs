namespace SMSMais.Core.Integracoes.SisregWeb;

/// <summary>
/// Resposta normalizada da consulta de paciente por CNS no SISREG III (CADSUS, tela
/// <c>cadweb50</c>). Espelha o shape da consulta por CPF (Hub) para o formulário de
/// Pacientes auto-preencher os campos. Diferente do CPF, o SISREG não exige data de
/// nascimento na busca — mas o CADSUS a devolve, então ela vem de brinde.
/// </summary>
public sealed record ConsultaCnsRespostaDto(
    string Cns,
    string Cpf,
    string Nome,
    /// <summary>Sexo canônico ("Masculino"/"Feminino") quando o CADSUS informa; null caso contrário.</summary>
    string? Sexo,
    DateOnly? DataNascimento,
    string? NomeMae);
