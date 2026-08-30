using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.Unidades;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Parser do combo de unidades do formulário <c>cons_agendas</c> — a lista que o "sincroniza tudo"
/// usa para descobrir a rede inteira em UMA requisição.
///
/// <para>O HTML abaixo espelha o formato real capturado no SISREG III: tags em <b>CAIXA ALTA</b>,
/// atributo <c>VALUE</c> maiúsculo, e o primeiro item sendo o placeholder
/// <c>&lt;OPTION value=""&gt;Selecione a unidade&lt;/OPTION&gt;</c>. Um parser sensível a caixa
/// devolveria zero unidades — e zero unidades aqui é indistinguível de "sessão caída", ou seja, o
/// lote simplesmente pararia de descobrir unidade nova sem ninguém entender por quê.</para>
/// </summary>
public class ConsAgendasUpsParserTests
{
    private const string FormularioHtml = """
        <html><body><form name='formulario' method='POST'>
        <SELECT style='width: 100%' TYPE="PICK" NAME="ups" onChange="ajax_populaProfUps(formulario.ups.value,formulario.cpf);">
        <OPTION value="">Selecione a unidade</OPTION>
        <OPTION VALUE="2266741">AMBULATORIO PERICLES SIQUEIRA FERREIRA</OPTION>
        <OPTION VALUE="3132358">CDT DR ALBERTO LUIS MACHADO BORGES</OPTION>
        <OPTION VALUE="2930242">CENTRO MATERNO INFANTIL</OPTION>
        </SELECT>
        <SELECT NAME="cpf"><OPTION value="">Selecione o profissional</OPTION></SELECT>
        </form></body></html>
        """;

    [Fact]
    public void Le_as_unidades_do_combo_ignorando_o_placeholder()
    {
        var unidades = ConsAgendasUpsParser.Ler(FormularioHtml);

        unidades.Should().HaveCount(3);
        unidades[0].Cnes.Should().Be("2266741");
        unidades[0].Nome.Should().Be("AMBULATORIO PERICLES SIQUEIRA FERREIRA");
        unidades.Should().OnlyContain(u => u.Cnes.Length == 7);
    }

    [Fact]
    public void Nao_confunde_o_combo_de_profissional_com_o_de_unidade()
    {
        // O formulário tem vários <select>; pegar o errado traria CPF no lugar de CNES.
        var unidades = ConsAgendasUpsParser.Ler(FormularioHtml);

        unidades.Should().NotContain(u => u.Nome.Contains("profissional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Decodifica_entidade_e_normaliza_espaco()
    {
        const string html = """
            <SELECT NAME="ups">
            <OPTION VALUE="1234567">UNIDADE   B&Aacute;SICA
            DE SAUDE</OPTION>
            </SELECT>
            """;

        var unidades = ConsAgendasUpsParser.Ler(html);

        unidades.Should().ContainSingle();
        unidades[0].Nome.Should().Be("UNIDADE BÁSICA DE SAUDE");
    }

    [Fact]
    public void Dedup_por_cnes()
    {
        const string html = """
            <SELECT NAME="ups">
            <OPTION VALUE="1234567">UNIDADE UM</OPTION>
            <OPTION VALUE="1234567">UNIDADE UM (REPETIDA)</OPTION>
            </SELECT>
            """;

        ConsAgendasUpsParser.Ler(html).Should().ContainSingle();
    }

    /// <summary>
    /// Sem o combo, devolve vazio — e quem chama trata como falha de sessão, nunca como
    /// "a rede não tem unidades". Distinguir os dois casos é o que impede o lote de concluir
    /// silenciosamente sem ter descoberto nada.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("<html><body>Sessao expirada</body></html>")]
    [InlineData("<SELECT NAME=\"outro\"><OPTION VALUE=\"1234567\">X</OPTION></SELECT>")]
    public void Sem_combo_de_unidade_devolve_vazio(string html)
    {
        ConsAgendasUpsParser.Ler(html).Should().BeEmpty();
    }
}
