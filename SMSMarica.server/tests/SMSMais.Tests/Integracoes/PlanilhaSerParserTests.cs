using FluentAssertions;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SerWeb.Varredura.Export;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Conversão das células do <c>historico-pesquisar.xls</c>.
///
/// <para>Os valores abaixo foram <b>medidos no arquivo real</b> baixado do SER em 06/08/2026 — não
/// são inventados. O arquivo em si não entra no repositório: tem nome, CNS e CID de paciente.</para>
/// </summary>
public class PlanilhaSerParserTests
{
    /// <summary>
    /// O SER exporta data como número de série do Excel. Se isso não for convertido, a coluna
    /// <c>Data da Solicitação</c> chega nula no espelho — e ela é o eixo de todo o fatiamento da
    /// varredura. O modo de falhar é silencioso: o parse de "46204,37" só devolve nulo.
    /// </summary>
    [Theory]
    [InlineData("46204.3728119560", "01/07/2026")]
    [InlineData("44776.5393079050", "03/08/2022")]
    [InlineData("44776.5476687152", "03/08/2022")]
    public void Serial_do_excel_vira_data_brasileira(string bruto, string esperado) =>
        PlanilhaSerParser.ConverterData(bruto).Should().Be(esperado);

    /// <summary>Se um dia o ExcelDataReader reconhecer o formato da célula e já entregar a data
    /// pronta, o valor passa direto — não pode ser convertido duas vezes.</summary>
    [Fact]
    public void Data_ja_formatada_passa_intacta() =>
        PlanilhaSerParser.ConverterData("03/08/2022").Should().Be("03/08/2022");

    [Fact]
    public void Celula_vazia_vira_nulo() =>
        PlanilhaSerParser.ConverterData("   ").Should().BeNull();

    /// <summary>
    /// Números que NÃO são data têm de sobreviver intactos. O ID da solicitação chega como número
    /// (<c>8000763</c>) e o CNS tem 15 dígitos — tratar qualquer número como serial de data
    /// transformaria identificador em data e ninguém veria o estrago.
    /// </summary>
    [Theory]
    [InlineData("8000763")]
    [InlineData("700501721613951")]
    [InlineData("3968616")]
    public void Numero_fora_da_faixa_de_data_nao_e_convertido(string bruto) =>
        PlanilhaSerParser.ConverterData(bruto).Should().Be(bruto);

    /// <summary>
    /// Cabeçalho com 4+ sinônimos mas SEM as colunas estruturais é recusado com nome e sobrenome.
    /// Sem a de ID, o filtro de linha descartaria tudo e <c>Ler</c> devolveria vazio sem exceção —
    /// que o varredor aplicaria como "cobertura completa", avançando o cursor. Achado do repasse
    /// de 07/08/2026.
    /// </summary>
    [Fact]
    public void Cabecalho_sem_coluna_estrutural_e_recusado_com_nome_e_sobrenome()
    {
        var semId = new Dictionary<string, int>
            { ["tipo"] = 0, ["cpf"] = 1, ["cns"] = 2, ["data_solicitacao"] = 3 };
        var semData = new Dictionary<string, int>
            { ["id"] = 0, ["tipo"] = 1, ["cpf"] = 2, ["cns"] = 3 };
        var completo = new Dictionary<string, int>
            { ["id"] = 0, ["data_solicitacao"] = 1, ["tipo"] = 2, ["cns"] = 3 };

        var acaoSemId = () => PlanilhaSerParser.GarantirColunasEstruturais(semId);
        var acaoSemData = () => PlanilhaSerParser.GarantirColunasEstruturais(semData);
        var acaoCompleta = () => PlanilhaSerParser.GarantirColunasEstruturais(completo);

        acaoSemId.Should().Throw<InvalidOperationException>().WithMessage("*ID Solicitação*");
        acaoSemData.Should().Throw<InvalidOperationException>().WithMessage("*Data da Solicitação*");
        acaoCompleta.Should().NotThrow();
    }

}
