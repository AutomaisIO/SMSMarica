using System.Text;
using FluentAssertions;
using Hl7.Fhir.Model;
using SMSMais.Core.Atendimentos;

namespace SMSMais.Tests.Cidadao;

public sealed class DocumentoAtendimentoHtmlTests
{
    private static Attachment Anexo(string tipo, string conteudo) =>
        new() { ContentType = tipo, Data = Encoding.UTF8.GetBytes(conteudo) };

    [Fact]
    public void Evolucao_em_texto_puro_vira_paragrafos_com_as_quebras_de_linha()
    {
        var html = DocumentoAtendimentoHtml.ParaExibir(Anexo(
            "text/plain",
            "PACIENTE REFERE DOR\r\nPA: 120X80\r\n\r\n\r\nCONDUTA: ALTA  \n"));

        html.Should().Be(
            "<div class=\"edoc-texto\"><p>PACIENTE REFERE DOR<br/>PA: 120X80</p><p>CONDUTA: ALTA</p></div>");
    }

    [Fact]
    public void Texto_puro_nao_vira_marcacao()
    {
        var html = DocumentoAtendimentoHtml.ParaExibir(Anexo(
            "text/plain; charset=utf-8", "GLICEMIA <70 & SINTOMAS <img src=x onerror=alert(1)>"));

        html.Should().NotContain("<img");
        html.Should().Contain("GLICEMIA &lt;70 &amp; SINTOMAS &lt;img src=x onerror=alert(1)&gt;");
    }

    [Fact]
    public void Html_dos_importadores_passa_como_esta()
    {
        const string boletim = "<h3>Anamnese</h3><p>DOR<br/>FEBRE</p>";

        DocumentoAtendimentoHtml.ParaExibir(Anexo("text/html", boletim)).Should().Be(boletim);
    }

    [Fact]
    public void Sem_conteudo_devolve_vazio()
    {
        DocumentoAtendimentoHtml.ParaExibir(null).Should().BeEmpty();
        DocumentoAtendimentoHtml.ParaExibir(Anexo("text/plain", " \r\n \n")).Should().BeEmpty();
    }
}
