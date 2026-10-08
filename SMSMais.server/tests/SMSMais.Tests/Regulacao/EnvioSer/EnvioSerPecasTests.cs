using PdfSharp.Pdf.IO;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Regulacao.EnvioSer;
using SMSMais.Core.Ser.Criacao;

namespace SMSMais.Tests.Regulacao.EnvioSer;

/// <summary>
/// As peças puras do envio ao SER: a regra de anexos que a própria tela do SER declara, a leitura
/// do número que o Gravar devolve e a aplicação dos pedaços A4J na página.
/// </summary>
public class EnvioSerPecasTests
{
    // PNG 1x1 válido — o menor arquivo de imagem que o PDFsharp aceita desenhar.
    private static readonly byte[] Png1x1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static ArquivoLido Imagem(string titulo) =>
        new(Guid.NewGuid(), "2026-10-02_004.png", titulo, "image/png", Png1x1);

    [Fact]
    public void Ate_dois_anexos_vao_como_estao_com_o_titulo_como_nome()
    {
        var saida = AnexosParaSer.Preparar([Imagem("RELATÓRIO MÉDICO"), Imagem("IDENTIFICAÇÃO")]);

        saida.Should().HaveCount(2);
        saida[0].Nome.Should().Be("RELATORIO MEDICO.png", "nome de foto de celular não diz nada a quem regula no SER");
        saida[1].Nome.Should().Be("IDENTIFICACAO.png");
        saida[0].Conteudo.Should().Equal(Png1x1);
    }

    [Fact]
    public void Mais_de_dois_anexos_viram_um_PDF_so_com_uma_pagina_por_imagem()
    {
        var tres = new[] { Imagem("A"), Imagem("B"), Imagem("C") };

        var saida = AnexosParaSer.Preparar(tres);

        // "No máximo dois arquivos por solicitação… inserir as imagens em um documento" (tela do SER).
        saida.Should().ContainSingle();
        saida[0].ContentType.Should().Be("application/pdf");
        saida[0].Origens.Should().BeEquivalentTo(tres.Select(a => a.Id));
        using var pdf = PdfReader.Open(new MemoryStream(saida[0].Conteudo), PdfDocumentOpenMode.Import);
        pdf.PageCount.Should().Be(3);
    }

    [Fact]
    public void Arquivo_acima_de_5MB_e_recusado_antes_de_tocar_o_SER()
    {
        var grande = new ArquivoLido(Guid.NewGuid(), "exame.pdf", "EXAME", "application/pdf", new byte[6 * 1024 * 1024]);

        var acao = () => AnexosParaSer.Preparar([grande]);

        acao.Should().Throw<ValidacaoException>().WithMessage("*EXAME.pdf*5 MB*");
    }

    [Theory]
    [InlineData("Solicitação de nº 8213344 salva com sucesso", "8213344")]
    [InlineData("Solicitação 8213344 gravada.", "8213344")]
    [InlineData("Registro nº 8213344 incluído", "8213344")]
    public void Le_o_numero_da_mensagem_do_Gravar(string mensagem, string esperado)
    {
        var html = $"<html><body><form id=\"form0\"><div id=\"form0:divMensagens\">{mensagem}</div></form></body></html>";

        SerCriacaoSolicitacao.NumeroGerado(html).Should().Be(esperado);
    }

    [Fact]
    public void Numero_curto_do_SERNIT_e_lido_tambem_do_msgErro()
    {
        // O SERNIT numera com 3–5 dígitos e também escreve no `form0:msgErro`.
        var html = "<html><body><form id=\"form0\"><span id=\"form0:msgErro\">Solicitação 4521 salva com sucesso</span></form></body></html>";

        SerCriacaoSolicitacao.NumeroGerado(html).Should().Be("4521");
        SerCriacaoSolicitacao.MensagemDaResposta(html).Should().Contain("4521");
    }

    [Fact]
    public void Botao_e_achado_pelo_rotulo_seja_link_do_SER_seja_input_do_SERNIT()
    {
        var ser = SerHtmlParser.Documento(
            "<form id=\"form0\"><a id=\"form0:j_id60\" title=\"Pesquisar\" href=\"#\"></a>"
            + "<a id=\"form0:j_id319\" href=\"#\">Gravar</a></form>");
        var sernit = SerHtmlParser.Documento(
            "<form id=\"form0\"><input type=\"button\" id=\"form0:j_id60\" name=\"form0:j_id60\" value=\"Pesquisar\" />"
            + "<input type=\"button\" id=\"form0:j_id274\" name=\"form0:j_id274\" value=\"Anexar Arquivo\" />"
            + "<input type=\"button\" id=\"formAnexar:j_id289\" name=\"formAnexar:j_id289\" value=\"Anexar\" /></form>");

        SerCriacaoSolicitacao.ControlePorRotulo(ser, "form0:", "Pesquisar").Should().Be("form0:j_id60");
        SerCriacaoSolicitacao.ControlePorRotulo(ser, "form0:", "Gravar").Should().Be("form0:j_id319");
        SerCriacaoSolicitacao.ControlePorRotulo(sernit, "form0:", "Anexar Arquivo").Should().Be("form0:j_id274");
        SerCriacaoSolicitacao.ControlePorRotulo(sernit, "form0:", "Anexar").Should().BeNull("o Anexar do modal é de outro form");
        SerCriacaoSolicitacao.ControlePorRotulo(sernit, "formAnexar:", "Anexar").Should().Be("formAnexar:j_id289");
    }

    [Fact]
    public async Task Redirect_por_cabecalho_sem_pagina_e_seguido_pela_sessao()
    {
        // O SERNIT responde a aba Editar com 302 para http:// e corpo vazio (08/10/2026).
        var abertos = new List<string>();
        Task<string> Abrir(string url, CancellationToken _)
        {
            abertos.Add(url);
            return Task.FromResult("<form id=\"form0\"></form>");
        }

        var seguido = await TelaCriacaoRedirect.SeguirAsync("http://x/editar.seam", string.Empty, Abrir, CancellationToken.None);
        var comPagina = await TelaCriacaoRedirect.SeguirAsync("http://x/editar.seam", "<form id=\"form0\">a</form>", Abrir, CancellationToken.None);
        var semLocation = await TelaCriacaoRedirect.SeguirAsync(null, "pedaço A4J", Abrir, CancellationToken.None);

        seguido.Should().Be("<form id=\"form0\"></form>");
        comPagina.Should().Be("<form id=\"form0\">a</form>", "resposta que já trouxe a página não é redirect");
        semLocation.Should().Be("pedaço A4J");
        abertos.Should().Equal("http://x/editar.seam");
    }

    [Fact]
    public void Mensagem_de_recusa_nao_tem_numero()
    {
        var html = "<html><body><form id=\"form0\"><div id=\"form0:divMensagens\">Consulta ou Exame é obrigatório.</div></form></body></html>";

        SerCriacaoSolicitacao.NumeroGerado(html).Should().BeNull();
    }

    [Fact]
    public void Pedaco_A4J_diz_quais_elementos_trocar()
    {
        var pedaco = SerHtmlParser.Documento(
            "<html><head><meta name=\"Ajax-Update-Ids\" content=\"form0:painelRecurso, form0:camposDinamicos\" /></head>"
            + "<body><div id=\"form0:painelRecurso\"></div></body></html>");

        SerCriacaoSolicitacao.IdsAtualizados(pedaco).Should().Equal("form0:painelRecurso", "form0:camposDinamicos");
    }

    [Fact]
    public void Evento_A4J_sai_do_select_e_tambem_do_radio()
    {
        const string html =
            "<select name=\"form0:comboRecurso\" onchange=\"A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:j_id57'})\"></select>"
            + "<input type=\"radio\" name=\"form0:unidadeDeOrigemIdentificada_radio\" value=\"false\" "
            + "onclick=\"A4J.AJAX.Submit('form0',event,{'similarityGroupingId':'form0:j_id270'})\" />";

        SerCriacaoSolicitacao.EventoDoControle(html, "form0:comboRecurso").Should().Be("form0:j_id57");
        SerCriacaoSolicitacao.EventoDoControle(html, "form0:unidadeDeOrigemIdentificada_radio").Should().Be("form0:j_id270");
    }
}

/// <summary>
/// O anexo que chegava ao SER sem arquivo (08/10/2026: PR-20 e PR-22 gravadas com "Nome do Arquivo"
/// vazio e download "Null").
/// </summary>
public class EnvioSerAnexoTests
{
    [Fact]
    public void Multipart_sai_como_o_do_navegador()
    {
        var campos = new Dictionary<string, string>
        {
            ["formAnexar"] = "formAnexar",
            ["javax.faces.ViewState"] = "j_id9",
        };

        var (corpo, limite) = SerWebSessao.MontarMultipartComoNavegador(
            campos, "formAnexar:upload:file", "RELATORIO MEDICO.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF]);
        var texto = System.Text.Encoding.Latin1.GetString(corpo);

        texto.Should().Contain($"--{limite}\r\nContent-Disposition: form-data; name=\"formAnexar\"\r\n\r\nformAnexar\r\n",
            "campo de texto: nome entre aspas e SEM Content-Type, como o navegador");
        texto.Should().Contain(
            "Content-Disposition: form-data; name=\"formAnexar:upload:file\"; filename=\"RELATORIO MEDICO.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n");
        texto.Should().NotContain("filename*", "o navegador não manda filename*; o .NET mandava");
        texto.Should().NotContain("text/plain");
        texto.IndexOf("upload:file", StringComparison.Ordinal).Should()
            .BeLessThan(texto.IndexOf("javax.faces.ViewState", StringComparison.Ordinal), "a ordem do DOM: o arquivo antes do ViewState");
        texto.Should().EndWith($"--{limite}--\r\n");
    }

    [Theory]
    [InlineData("<span id=\"_richfaces_file_upload_size_restricted\"></span>", "arquivo acima do tamanho permitido")]
    [InlineData("<span id=\"_richfaces_file_upload_forbidden\"></span>", "tipo de arquivo não permitido")]
    [InlineData("<span id=\"_richfaces_file_upload_stopped\"></span>", "upload interrompido")]
    [InlineData("<input name=\"javax.faces.ViewState\" value=\"j_id10\" />", null)]
    public void Le_a_recusa_que_o_SER_declara_no_upload(string html, string? esperado)
    {
        SerWebSessao.RecusaDoUpload(html).Should().Be(esperado);
    }

    [Fact]
    public void Linha_da_grade_sem_nome_de_arquivo_nao_conta_como_anexo()
    {
        // A grade como o SER a mostrou na PR-20: duas linhas, "Nome do Arquivo" vazio.
        var pagina = SerHtmlParser.Documento(
            "<table id=\"form0:anexoList\"><thead><tr><th>Data</th><th>Nome do Arquivo</th><th>Usuário</th><th>Ação</th></tr></thead>"
            + "<tbody><tr><td>23:23 - 07/10/2026</td><td></td><td>FULANO</td><td><a href=\"#\">Abrir</a></td></tr>"
            + "<tr><td>23:23 - 07/10/2026</td><td>IDENTIFICACAO.jpg</td><td>FULANO</td><td><a href=\"#\">Abrir</a></td></tr></tbody></table>");

        var nomes = SerCriacaoSolicitacao.NomesDaGradeDeAnexos(pagina);

        nomes.Should().Equal("", "IDENTIFICACAO.jpg");
        nomes.Should().NotContain("RELATORIO MEDICO.jpg", "linha de nome vazio é arquivo que não chegou");
    }
}
