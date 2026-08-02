using SMSMarica.Core.Integracoes.Proxy.Motores;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// Classificação do <c>status:false</c> do Hub do Desenvolvedor: negativa AUTORITATIVA (os
/// dados não conferem — resposta final) × problema do FORNECEDOR (sem saldo, token, instável
/// — retenta e cai para o fallback).
///
/// Errar para o lado do fornecedor custa caro em dois lugares: no cadastro, o usuário leva
/// 3 tentativas + fallback antes de ouvir "não confere"; na arbitragem de divergências
/// (ADR-0039) são 12 chamadas externas por caso. Foi exatamente o que aconteceu em 02/08:
/// o Hub responde <b>"Data Nascimento invalida"</b> (sem o "de") e a allowlist só tinha
/// "data de nascimento invalida".
/// </summary>
public class NegativaAutoritativaHubTests
{
    [Theory]
    // A redação real do Hub que quebrou em produção:
    [InlineData("NOK", "Data Nascimento invalida")]
    [InlineData("NOK", "Data de Nascimento invalida")]
    [InlineData("NOK", "DATA NASCIMENTO INVÁLIDA")]
    [InlineData("NOK", "CPF invalido")]
    [InlineData("NOK", "CPF ou data de nascimento nao conferem")]
    [InlineData("NOK", "Contribuinte nao encontrado")]
    [InlineData("", "Dados divergentes")]
    [InlineData("NOK", "cpf incorreto")]
    public void Dados_que_nao_conferem_sao_negativa_final(string retorno, string mensagem) =>
        Assert.True(HubDoDesenvolvedor.EhNegativaAutoritativa(retorno, mensagem));

    [Theory]
    // Problema do fornecedor: NUNCA pode virar "seu CPF é inválido" para o usuário.
    [InlineData("NOK", "Token invalido")]
    [InlineData("NOK", "Sem saldo para esta consulta")]
    [InlineData("NOK", "Creditos insuficientes")]
    [InlineData("NOK", "Requisicao invalida")]
    [InlineData("NOK", "Servico temporariamente indisponivel")]
    [InlineData("NOK", "Tente novamente em instantes")]
    [InlineData("", "")]
    public void Problema_do_fornecedor_nao_e_negativa(string retorno, string mensagem) =>
        Assert.False(HubDoDesenvolvedor.EhNegativaAutoritativa(retorno, mensagem));
}
