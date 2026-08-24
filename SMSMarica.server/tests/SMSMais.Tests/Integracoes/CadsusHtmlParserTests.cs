using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Testes puros do parser da ficha CADSUS (SISREG III <c>cadweb50</c>). O HTML abaixo
/// espelha a estrutura real (rótulo numa linha, valor na linha seguinte, alinhados por
/// coluna) com dados SINTÉTICOS — nunca PII real.
/// </summary>
public class CadsusHtmlParserTests
{
    // Estrutura fiel à tela: seção → linha de rótulos → linha de valores.
    private const string FichaHtml = """
        <html><body>
        <table class="table_listagem">
          <tr><td class="td_titulo_tabela" colspan="4">Dados Pessoais:</td></tr>
          <tr><td colspan="4">CNS:</td></tr>
          <tr><td colspan="2">700000000000001</td></tr>
          <tr><td colspan="2">Nome:</td><td colspan="2">Nome Social / Apelido:</td></tr>
          <tr><td colspan="2">FULANO DE TAL SILVA</td><td colspan="2">---</td></tr>
          <tr><td colspan="2">Nome da Mãe:</td><td colspan="2">Nome do Pai:</td></tr>
          <tr><td colspan="2">MARIA DE TAL SILVA</td><td colspan="2">SEM INFORMAÇÃO</td></tr>
          <tr><td colspan="2">Sexo:</td><td colspan="2">Raça:</td></tr>
          <tr><td colspan="2">MASCULINO</td><td colspan="2">SEM INFORMACAO</td></tr>
          <tr><td colspan="2">Data de Nascimento:</td><td colspan="2">Tipo Sanguíneo:</td></tr>
          <tr><td colspan="2">14/02/1983 (43 anos)</td><td colspan="2">---</td></tr>
          <tr><td colspan="2">Nacionalidade:</td><td colspan="2">Município de Nascimento:</td></tr>
          <tr><td colspan="2">BRASILEIRA</td><td colspan="2">--- - ---</td></tr>
        </table>
        <table class="table_listagem">
          <tr><td class="td_titulo_tabela" colspan="4">Endereço:</td></tr>
          <tr><td colspan="2">Tipo Logradouro:</td><td colspan="2">Logradouro:</td></tr>
          <tr><td colspan="2">AVENIDA</td><td colspan="2">RUA EXEMPLO</td></tr>
          <tr><td colspan="2">Bairro:</td><td colspan="2">CEP:</td></tr>
          <tr><td colspan="2">CENTRO</td><td colspan="2">12345-678</td></tr>
        </table>
        <table class="table_listagem">
          <tr><td class="td_titulo_tabela" colspan="4">Documentos:</td></tr>
          <tr><td colspan="4">CPF:</td></tr>
          <tr><td colspan="4">123.456.789-09</td></tr>
        </table>
        </body></html>
        """;

    [Fact]
    public void Parse_extrai_campos_nucleares_da_ficha()
    {
        var reg = CadsusHtmlParser.Parse(FichaHtml);

        reg.Should().NotBeNull();
        reg!.Cns.Should().Be("700000000000001");
        reg.Cpf.Should().Be("12345678909");
        reg.Nome.Should().Be("FULANO DE TAL SILVA");
        reg.Sexo.Should().Be("Masculino");
        reg.DataNascimento.Should().Be(new DateOnly(1983, 2, 14));
        reg.NomeMae.Should().Be("MARIA DE TAL SILVA");
    }

    [Fact]
    public void Parse_normaliza_ausencias_para_null()
    {
        var reg = CadsusHtmlParser.Parse(FichaHtml);

        // "SEM INFORMAÇÃO"/"---" viram null.
        reg!.NomePai.Should().BeNull();
        reg.Raca.Should().BeNull();
        reg.MunicipioNascimento.Should().BeNull();
    }

    [Fact]
    public void Parse_mapeia_endereco_mesmo_sem_expor_no_auto_preenchimento()
    {
        // Endereço é mapeado (para uso futuro), mas não retornado ao formulário.
        var reg = CadsusHtmlParser.Parse(FichaHtml);

        reg!.Endereco.Logradouro.Should().Be("RUA EXEMPLO");
        reg.Endereco.Bairro.Should().Be("CENTRO");
        reg.Endereco.Cep.Should().Be("12345678");
    }

    [Fact]
    public void Parse_retorna_null_para_html_sem_paciente()
    {
        CadsusHtmlParser.Parse("<html><body><h1>Nada aqui</h1></body></html>").Should().BeNull();
        CadsusHtmlParser.Parse("").Should().BeNull();
    }

    [Fact]
    public void EhTelaLogin_detecta_pagina_de_login()
    {
        var login = "<form name=\"formLogin\"><input name=\"senha_256\" type=\"hidden\"/></form>";
        CadsusHtmlParser.EhTelaLogin(login).Should().BeTrue();
        CadsusHtmlParser.EhTelaLogin(FichaHtml).Should().BeFalse();
    }

    [Fact]
    public void SessaoInvalida_detecta_login_e_sessao_finalizada()
    {
        var login = "<form name=\"formLogin\"><input name=\"senha_256\"/></form>";
        // Página real de sessão única derrubada (acentos vêm como entidades HTML).
        var finalizada = "Erro de Sistema Motivo: Este operador efetuou logon em outra esta&ccedil;&atilde;o "
                       + "de trabalho. Sua sess&#227;o foi finalizada pelo servidor.";
        CadsusHtmlParser.SessaoInvalida(login).Should().BeTrue();
        CadsusHtmlParser.SessaoInvalida(finalizada).Should().BeTrue();
        CadsusHtmlParser.SessaoInvalida(FichaHtml).Should().BeFalse();
    }
}
