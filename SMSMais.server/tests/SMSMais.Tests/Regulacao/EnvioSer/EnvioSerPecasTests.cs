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
