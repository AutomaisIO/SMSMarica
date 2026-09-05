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

    /// <summary>Campo que nasce preenchido (não existia aqui) é alteração legítima, com "antes" vazio —
    /// é o caso das solicitações antigas que nunca souberam quem executa.</summary>
    [Fact]
    public void Campo_que_estava_vazio_aqui_e_veio_do_sisreg_e_alteracao()
    {
        var alteracao = Assert.Single(
            ComparadorMarcacao.Comparar(Foto(cpf: null), Foto(cpf: "22222222222")));

        Assert.Equal(TipoAlteracaoAgenda.Executante, alteracao.Tipo);
        Assert.Null(alteracao.Antes);
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
