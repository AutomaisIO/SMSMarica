using Ganss.Xss;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMSMarica.Core;

namespace SMSMais.Tests.Laudos;

/// <summary>
/// O laudo passa pelo <see cref="IHtmlSanitizer"/> antes de ser gravado. Se a
/// marca <c>class="laudo-tabela"</c> ou a largura <c>colwidth</c> forem removidas
/// aqui, a tabela volta a sair como caixas empilhadas no PDF — e em SILÊNCIO,
/// sem erro nenhum. Estes testes travam a whitelist.
///
/// <para>
/// Resolve o sanitizer do container REAL (<c>AddCore</c>), não uma cópia da
/// configuração — cópia não protege contra alguém mexer no registro de produção.
/// </para>
/// </summary>
public class LaudoHtmlSanitizerTabelaTests
{
    private static IHtmlSanitizer Sanitizer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();
        services.AddCore(configuration);
        return services.BuildServiceProvider().GetRequiredService<IHtmlSanitizer>();
    }

    [Fact]
    public void Preserva_a_marca_de_tabela_de_dados_e_a_largura_das_colunas()
    {
        const string html = """
            <table class="laudo-tabela"><tbody>
            <tr><th colwidth="119" style="width: 17%"><p>Região estudada</p></th><th colwidth="98"><p>Sítio</p></th></tr>
            <tr><td><p>Coluna Lombar</p></td><td><p>L1 a L4</p></td></tr>
            </tbody></table>
            """;

        var limpo = Sanitizer().Sanitize(html);

        limpo.Should().Contain("laudo-tabela", "sem a classe o PDF cai no caminho de layout");
        limpo.Should().Contain("colwidth=\"119\"", "é a única largura que sobrevive ao round-trip pelo TipTap");
        limpo.Should().Contain("colwidth=\"98\"");
        limpo.Should().Contain("width: 17%");
        limpo.Should().Contain("<th", "cabeçalho precisa continuar sendo th para virar Header() no PDF");
        limpo.Should().Contain("Região estudada");
    }

    [Fact]
    public void Preserva_o_colgroup_que_o_tiptap_serializa()
    {
        const string html = """
            <table class="laudo-tabela"><colgroup><col style="width: 119px"></colgroup>
            <tbody><tr><th colwidth="119"><p>Região</p></th></tr></tbody></table>
            """;

        var limpo = Sanitizer().Sanitize(html);

        limpo.Should().Contain("<colgroup");
        limpo.Should().Contain("<col");
    }

    [Fact]
    public void Continua_barrando_script_e_atributo_de_evento()
    {
        // A liberação de class/colwidth não pode ter aberto a porteira.
        const string html = """
            <table class="laudo-tabela" onclick="roubar()"><tbody>
            <tr><td onmouseover="x()"><p>ok</p><script>alert(1)</script></td></tr>
            </tbody></table>
            """;

        var limpo = Sanitizer().Sanitize(html);

        limpo.Should().NotContain("script");
        limpo.Should().NotContain("onclick");
        limpo.Should().NotContain("onmouseover");
        limpo.Should().Contain("laudo-tabela");
    }
}
