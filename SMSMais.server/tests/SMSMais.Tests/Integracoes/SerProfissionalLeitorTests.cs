using System.Text;

using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Profissionais;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// A pesquisa de Cadastro → Profissionais do SER (ADR-0065), com HTML sintético no formato medido
/// em 01/10/2026 (<c>Automais.SER/probe_profissional_saude.py</c>). Dados inventados.
/// </summary>
public class SerProfissionalLeitorTests
{
    private const string Pagina = """
        <form id="form0" action="/ser/pages/cadastro/profissionalSaude/profissional-pesquisar.seam">
          <input type="text" name="form0:nome" value="" />
          <input type="submit" name="form0:j_id36" value="Pesquisar" class="botao" />
          <table id="form0:listagem"><tbody id="form0:listagem:tb">
            <tr>
              <td id="form0:listagem:20:j_id40"><a href="#">Editar</a></td>
              <td>123.456.789-09</td><td>5212345</td><td>CRM</td>
              <td>  MARIA   EXEMPLO  DA SILVA </td>
              <td><input type="checkbox" checked="checked" disabled="disabled" /></td>
            </tr>
            <tr>
              <td id="form0:listagem:21:j_id40"><a href="#">Editar</a></td>
              <td></td><td></td><td>CNS</td>
              <td>J. EXEMPLO</td>
              <td><input type="checkbox" disabled="disabled" /></td>
            </tr>
            <tr>
              <td id="form0:listagem:22:j_id40"><a href="#">Editar</a></td>
              <td></td><td></td><td>CNS</td><td></td><td><input type="checkbox" /></td>
            </tr>
          </tbody></table>
        </form>
        """;

    [Fact]
    public void Le_as_linhas_com_indice_absoluto_cpf_so_digitos_e_ativo()
    {
        var linhas = SerProfissionalLeitor.LerLinhas(Pagina);

        // A linha sem nome (o SER tem 3 assim) não entra: não há como escolher um médico sem nome.
        linhas.Should().HaveCount(2);

        var (i0, l0) = linhas[0];
        i0.Should().Be(20, "o índice é absoluto — é por ele que a página nova é reconhecida");
        l0.Cpf.Should().Be("12345678909");
        l0.Documento.Should().Be("5212345");
        l0.TipoDocumento.Should().Be("CRM");
        l0.Nome.Should().Be("MARIA EXEMPLO DA SILVA");
        l0.Ativo.Should().BeTrue();

        var (_, l1) = linhas[1];
        l1.Cpf.Should().BeNull();
        l1.Documento.Should().BeNull();
        l1.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Acha_o_botao_Pesquisar_pelo_rotulo_e_nao_pelo_j_id()
    {
        SerProfissionalLeitor.BotaoPesquisar(SerHtmlParser.Documento(Pagina)).Should().Be("form0:j_id36");
    }

    [Fact]
    public void Decodifica_resposta_A4J_em_ISO_8859_1_mesmo_declarada_UTF8()
    {
        // O SER declara UTF-8 na resposta A4J e manda bytes ISO-8859-1: decodificar como UTF-8
        // estragaria todo nome com acento.
        var latin1 = Encoding.Latin1.GetBytes("JOSÉ CONCEIÇÃO");
        SerProfissionalLeitor.Decodificar(latin1).Should().Be("JOSÉ CONCEIÇÃO");

        var utf8 = Encoding.UTF8.GetBytes("JOSÉ CONCEIÇÃO");
        SerProfissionalLeitor.Decodificar(utf8).Should().Be("JOSÉ CONCEIÇÃO");
    }
}
