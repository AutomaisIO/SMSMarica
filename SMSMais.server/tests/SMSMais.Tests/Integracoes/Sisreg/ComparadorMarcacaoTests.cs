using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Detecção do que o SISREG mudou numa solicitação já importada.
///
/// <para><b>O problema que isto resolve:</b> até 05/09/2026, encontrar um nº de solicitação já
/// conhecido era motivo para pular a linha. Barato e cego — uma consulta remarcada no SISREG deixava
/// nosso banco com a data velha <b>para sempre</b>, sem erro, sem log, sem ninguém saber. Uma agenda
/// montada sobre isso mostraria o paciente no dia errado com cara de certeza.</para>
///
/// <para><b>O risco do remédio é o ruído.</b> Se a comparação acusar mudança à toa, a fila do
/// regulador enche de linha que não é nada e ninguém olha mais — o que é pior que não ter fila.
/// Por isso os testes de "não alterou" são tão importantes quanto os de "alterou".</para>
/// </summary>
public class ComparadorMarcacaoTests
{
    private static readonly DateTime Dia10 = new(2026, 9, 10, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dia17 = new(2026, 9, 17, 14, 0, 0, DateTimeKind.Utc);

    private static FotoMarcacao Foto(
        DateTime? data = null,
        string? cpf = "12345678901",
        string? codigo = "0229000",
        string? nome = "ULTRASSONOGRAFIA") =>
        new(data ?? Dia10, cpf, codigo, nome);

    [Fact]
    public void Nada_mudou_nao_gera_alteracao()
    {
        Assert.Empty(ComparadorMarcacao.Comparar(Foto(), Foto()));
    }

    /// <summary>A que importa: o paciente tem na mão um dia que não vale mais.</summary>
    [Fact]
    public void Remarcacao_gera_alteracao_de_data_com_antes_e_depois()
    {
        var alteracao = Assert.Single(
            ComparadorMarcacao.Comparar(Foto(data: Dia10), Foto(data: Dia17)));

        Assert.Equal(TipoAlteracaoAgenda.DataHora, alteracao.Tipo);
        Assert.Equal("10/09/2026 14:00", alteracao.Antes);
        Assert.Equal("17/09/2026 14:00", alteracao.Depois);
    }

    [Fact]
    public void Troca_de_executante_gera_alteracao()
    {
        var alteracao = Assert.Single(
            ComparadorMarcacao.Comparar(Foto(cpf: "11111111111"), Foto(cpf: "22222222222")));

        Assert.Equal(TipoAlteracaoAgenda.Executante, alteracao.Tipo);
    }

    /// <summary>
    /// Código e nome do procedimento mudando juntos são <b>um</b> fato, não dois. Registrar duas
    /// linhas faria a contagem de alterações mentir e a fila parecer o dobro do que é.
    /// </summary>
    [Fact]
    public void Codigo_e_nome_do_procedimento_juntos_contam_como_uma_alteracao()
    {
        var alteracoes = ComparadorMarcacao.Comparar(
            Foto(codigo: "0229000", nome: "ULTRASSONOGRAFIA"),
            Foto(codigo: "1402000", nome: "TOMOGRAFIA"));

        var alteracao = Assert.Single(alteracoes);
        Assert.Equal(TipoAlteracaoAgenda.Procedimento, alteracao.Tipo);
    }

    /// <summary>Nome mudando sozinho (o SISREG renomeou) ainda é alteração de procedimento.</summary>
    [Fact]
    public void Nome_do_procedimento_sozinho_gera_alteracao()
    {
        var alteracao = Assert.Single(ComparadorMarcacao.Comparar(
            Foto(nome: "ULTRASSONOGRAFIA"), Foto(nome: "ULTRASSONOGRAFIA OBSTETRICA")));

        Assert.Equal(TipoAlteracaoAgenda.Procedimento, alteracao.Tipo);
    }

    /// <summary>
    /// Campo que o SISREG deixou de mandar NÃO é alteração. O código do procedimento vem vazio em
    /// cerca de um terço das linhas — ler isso como "mudou para nada" encheria a fila de fantasmas
    /// e, pior, apagaria dado bom ao aplicar.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Campo_vazio_no_arquivo_nao_e_alteracao(string? vazio)
    {
        Assert.Empty(ComparadorMarcacao.Comparar(
            Foto(codigo: "0229000", nome: "ULTRASSONOGRAFIA"),
            Foto(codigo: vazio, nome: "ULTRASSONOGRAFIA")));
    }

    [Fact]
    public void Data_ausente_no_arquivo_nao_apaga_a_que_temos()
    {
        Assert.Empty(ComparadorMarcacao.Comparar(Foto(data: Dia10), Foto(data: null)));
    }

    /// <summary>Diferença só de espaço ou caixa é formatação, não remarcação.</summary>
    [Theory]
    [InlineData("ULTRASSONOGRAFIA", " ULTRASSONOGRAFIA ")]
    [InlineData("ULTRASSONOGRAFIA", "ultrassonografia")]
    public void Diferenca_de_espaco_ou_caixa_nao_e_alteracao(string antes, string depois)
    {
        Assert.Empty(ComparadorMarcacao.Comparar(Foto(nome: antes), Foto(nome: depois)));
    }

    /// <summary>
    /// <b>Campo que passa a vir preenchido não é troca</b> — e este teste era o oposto disto.
    ///
    /// <para>A regra anterior ("campo que nasce preenchido é alteração legítima, com antes vazio")
    /// parecia razoável no papel e foi desmentida pela produção: em 06/09/2026 a fila do regulador
    /// amanheceu com <b>644 linhas de "trocou o procedimento"</b>, e a distribuição não deixou
    /// dúvida — <b>644 de 644 eram <c>vazio → código</c></b>, nenhuma era <c>código → outro
    /// código</c>. Ninguém trocou procedimento de ninguém: o SISREG passou a mandar o <c>pa</c> que
    /// vinha em branco em ~1/3 das linhas. Junto com 702 cancelamentos falsos, isso levou a fila a
    /// 1.378 pendências das quais ~34 eram reais — e fila que ninguém consegue ler não protege
    /// paciente nenhum.</para>
    ///
    /// <para>É notícia sobre o ARQUIVO, não sobre a agenda. O valor entra na solicitação na
    /// importação normal; o que não entra é uma linha de fila pedindo providência.</para>
    /// </summary>
    [Fact]
    public void Campo_que_passou_a_vir_preenchido_nao_e_alteracao()
    {
        Assert.Empty(ComparadorMarcacao.Comparar(Foto(cpf: null), Foto(cpf: "22222222222")));
        Assert.Empty(ComparadorMarcacao.Comparar(Foto(codigo: ""), Foto(codigo: "1402147")));
    }

    /// <summary>O que continua sendo alteração: valor real trocado por outro valor real.</summary>
    [Fact]
    public void Troca_entre_dois_valores_preenchidos_continua_sendo_alteracao()
    {
        var alteracao = Assert.Single(
            ComparadorMarcacao.Comparar(Foto(cpf: "11111111111"), Foto(cpf: "22222222222")));

        Assert.Equal(TipoAlteracaoAgenda.Executante, alteracao.Tipo);
        Assert.Equal("11111111111", alteracao.Antes);
        Assert.Equal("22222222222", alteracao.Depois);
    }

    /// <summary>Mudanças independentes são fatos independentes: remarcar E trocar o médico são
    /// duas providências diferentes para quem vai tratar.</summary>
    [Fact]
    public void Alteracoes_independentes_viram_linhas_separadas()
    {
        var alteracoes = ComparadorMarcacao.Comparar(
            Foto(data: Dia10, cpf: "11111111111"),
            Foto(data: Dia17, cpf: "22222222222"));

        Assert.Equal(2, alteracoes.Count);
        Assert.Contains(alteracoes, a => a.Tipo == TipoAlteracaoAgenda.DataHora);
        Assert.Contains(alteracoes, a => a.Tipo == TipoAlteracaoAgenda.Executante);
    }
}
