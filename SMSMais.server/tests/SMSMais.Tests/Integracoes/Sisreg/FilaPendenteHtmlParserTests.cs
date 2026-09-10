using SMSMais.Core.Integracoes.SisregWeb.Fila;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Leitura da fila do SISREG (<c>gerenciador_solicitacao</c>) — quem pediu e ainda não foi
/// agendado.
///
/// <para><b>O HTML dos testes é sintético, mas a ESTRUTURA é real</b>: copiada célula a célula de
/// capturas de 05/09/2026 (352, 3.669 e 15.505 linhas). Os valores são inventados de propósito —
/// as capturas têm nome, CNS e telefone de gente de verdade e não entram no repositório.</para>
///
/// <para>Dois campos aqui existem contra o que a documentação do laboratório afirmava, e são o que
/// torna a lista de espera utilizável: <b>o CNS</b> (no <c>title</c> da célula do paciente, 100%
/// das linhas conferidas) e <b>o risco</b> (imagem + <c>title</c> numérico; o doc dizia "sempre
/// vazio", a amostra tinha 10 vermelhos em 352).</para>
/// </summary>
public class FilaPendenteHtmlParserTests
{
    /// <summary>Uma linha com a mesma forma da real: 12 TDs, title na 3ª e na 4ª, img no risco.</summary>
    private static string Linha(
        string codigo = "576930306",
        string data = "02/01/2025",
        string risco = "2",
        string cor = "verde",
        string nome = "FULANO DE TAL DA SILVA",
        string cns = "700000000000001",
        string mae = "BELTRANA DE TAL",
        string nascimento = "15/03/1972",
        string telefone = "(21) 2637-1713<br>(21) 99999-8888",
        string municipio = "MARICA",
        string idade = "53 anos",
        string procedimento = "0301010072 - CONSULTA EM CARDIOLOGIA - AMB",
        string cid = "L03",
        string solicitante = "UNIDADE DE SAUDE DA FAMILIA TESTE",
        string executante = "---",
        string situacao = "SOL/PEN/REG") =>
        $"""
        <TR onClick="visualizaFicha({codigo});" class="par_tr linha_selecionavel">
        <TD align="center">{codigo}</TD>
        <TD align="center">{data}</TD>
        <TD align="center" title="{risco}" ><img src=/imagens/{cor}.png></TD>
        <TD align="left" title="N&uacute;mero CNS: {cns} &#10;Nome Paciente: {nome} &#10;Nome da M&atilde;e: {mae} &#10;Data Nascimento: {nascimento}">{nome}</TD>
        <TD align="center">{telefone}</TD>
        <TD align="left">{municipio}</TD>
        <TD align="center">{idade}</TD>
        <TD align="left">{procedimento}</TD>
        <TD align="center" title="{cid} - DESCRICAO DO CID">{cid}</TD>
        <TD align="left">{solicitante}</TD>
        <TD align="left">{executante}</TD>
        <TD align="left" title="SOLICITACAO / PENDENTE / REGULACAO">{situacao}</TD>
        </TR>
        """;

    /// <summary>
    /// O topo da página traz a DEFINIÇÃO da função <c>visualizaFicha</c> e o cabeçalho da tabela.
    /// Contar "visualizaFicha" no HTML inteiro superestima em 3 — o parser tem de casar só a
    /// chamada com número dentro de um TR.
    /// </summary>
    private static string Pagina(params string[] linhas) =>
        """
        <html><head><script>
        function visualizaFicha(co_solic) { frm.etapa.value = 'VISUALIZAR_FICHA'; frm.submit(); }
        function exibirFichaReduzida() { }
        function exibirFichaCompleta() { }
        </script></head><body>
        <TABLE><TR class="tr_titulo">
        <TD class="td_titulo_campo">C&oacute;d. Solicita&ccedil;&atilde;o</TD>
        <TD class="td_titulo_campo">Data da Solicita&ccedil;&atilde;o</TD>
        <TD class="td_titulo_campo">Risco</TD>
        <TD class="td_titulo_campo">Paciente</TD>
        </TR>
        """ + string.Join("\n", linhas) + "</TABLE></body></html>";

    [Fact]
    public void Le_a_linha_inteira_com_os_doze_campos()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha())));

        Assert.Equal("576930306", r.CodigoSolicitacao);
        Assert.Equal(new DateOnly(2025, 1, 2), r.DataSolicitacao);
        Assert.Equal(2, r.Risco);
        Assert.Equal("FULANO DE TAL DA SILVA", r.PacienteNome);
        Assert.Equal("700000000000001", r.Cns);
        Assert.Equal("BELTRANA DE TAL", r.NomeMae);
        Assert.Equal(new DateOnly(1972, 3, 15), r.DataNascimento);
        Assert.Equal(53, r.IdadeAnos);
        Assert.Equal("MARICA", r.Municipio);
        Assert.Equal("0301010072", r.ProcedimentoCodigo);
        Assert.Equal("CONSULTA EM CARDIOLOGIA - AMB", r.ProcedimentoNome);
        Assert.Equal("L03", r.CidCodigo);
        Assert.Equal("UNIDADE DE SAUDE DA FAMILIA TESTE", r.UnidadeSolicitante);
        Assert.Equal("SOL/PEN/REG", r.Situacao);
    }

    /// <summary>
    /// O CNS é o que liga esta fila ao nosso cadastro. Sem ele a lista seria só nomes soltos —
    /// e o laboratório tinha concluído que ele não vinha na listagem.
    /// </summary>
    [Fact]
    public void O_CNS_sai_do_title_e_nao_do_texto_da_celula()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha(cns: "706070809010203"))));

        Assert.Equal("706070809010203", r.Cns);
    }

    [Theory]
    [InlineData("0", "vermelho", 0)]
    [InlineData("1", "amarelo", 1)]
    [InlineData("2", "verde", 2)]
    [InlineData("3", "azul", 3)]
    public void Risco_vem_do_title_numerico_nao_da_cor(string title, string cor, int esperado)
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha(risco: title, cor: cor))));

        Assert.Equal(esperado, r.Risco);
    }

    /// <summary>Descartar um número é perder contato: os dois ficam.</summary>
    [Fact]
    public void Dois_telefones_na_mesma_celula_nao_grudam_num_numero_so()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(
            Pagina(Linha(telefone: "(21) 2637-1713<br>(21) 99999-8888"))));

        Assert.NotNull(r.Telefone);
        Assert.Contains("2637-1713", r.Telefone);
        Assert.Contains("99999-8888", r.Telefone);
        Assert.DoesNotContain("1713(21)", r.Telefone);
    }

    /// <summary>
    /// "---" é como o SISREG escreve "não se aplica" — quem ainda não foi regulado não tem
    /// executante. Guardar o traço faria a coluna parecer preenchida.
    /// </summary>
    [Fact]
    public void Traco_do_SISREG_vira_nulo()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha(municipio: "---"))));

        Assert.Null(r.Municipio);
    }

    [Fact]
    public void A_definicao_da_funcao_JS_no_topo_nao_vira_linha()
    {
        // A página tem 3 ocorrências de "visualizaFicha"/"exibirFicha" antes da primeira linha.
        var r = FilaPendenteHtmlParser.Ler(Pagina(Linha(), Linha(codigo: "576930307")));

        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void Pagina_vazia_ou_sem_linhas_devolve_lista_vazia()
    {
        Assert.Empty(FilaPendenteHtmlParser.Ler(""));
        Assert.Empty(FilaPendenteHtmlParser.Ler(Pagina()));
        Assert.Empty(FilaPendenteHtmlParser.Ler("<html>Nenhum registro encontrado</html>"));
    }

    /// <summary>Uma coluna deslocada gravaria telefone no lugar de idade — pular é melhor.</summary>
    [Fact]
    public void Linha_com_menos_de_doze_celulas_e_pulada()
    {
        var truncada = """
            <TR onClick="visualizaFicha(999);"><TD>999</TD><TD>01/01/2026</TD></TR>
            """;

        Assert.Empty(FilaPendenteHtmlParser.Ler(Pagina(truncada)));
    }

    /// <summary>
    /// Este é o caso REAL, não a exceção: nas 15.502 linhas conferidas o SISREG manda só o nome, e
    /// o hífen faz parte dele. Sem a exigência de prefixo todo-dígito, "GRUPO" viraria código de
    /// procedimento — e o casamento com a oferta passaria a ser por um código inventado.
    /// </summary>
    [Theory]
    [InlineData("GRUPO - PEQUENAS CIRURGIAS - LOCAL")]
    [InlineData("CONSULTA EM OFTALMOLOGIA - PEDIATRA")]
    [InlineData("GRUPO - DIAGNOSTICO POR RESSONANCIA MAGNETICA - PPI")]
    public void Nome_com_hifen_nao_vira_codigo_de_procedimento(string procedimento)
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha(procedimento: procedimento))));

        Assert.Null(r.ProcedimentoCodigo);
        Assert.Equal(procedimento, r.ProcedimentoNome);
    }

    /// <summary>Se um dia o SISREG mandar o código, o parser o aproveita — mas não conta com ele.</summary>
    [Fact]
    public void Codigo_numerico_na_frente_e_aproveitado_quando_existir()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(
            Pagina(Linha(procedimento: "0301010072 - CONSULTA EM CARDIOLOGIA"))));

        Assert.Equal("0301010072", r.ProcedimentoCodigo);
        Assert.Equal("CONSULTA EM CARDIOLOGIA", r.ProcedimentoNome);
    }

    [Fact]
    public void Idade_em_ano_singular_tambem_e_lida()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(Pagina(Linha(idade: "1 ano"))));

        Assert.Equal(1, r.IdadeAnos);
    }

    [Fact]
    public void Entidades_html_do_nome_sao_resolvidas()
    {
        var r = Assert.Single(FilaPendenteHtmlParser.Ler(
            Pagina(Linha(nome: "JO&Atilde;O DA CONCEI&Ccedil;&Atilde;O"))));

        Assert.Equal("JOÃO DA CONCEIÇÃO", r.PacienteNome);
    }
}
