using System.Text.Json;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestPDF.Infrastructure;
using SMSMais.Core.Institucional;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Laudos.Assinatura.Nuvem;
using SMSMais.Core.Laudos.Configuracao;
using SMSMais.Core.Laudos.Configuracao.Dtos;
using SMSMais.Core.Laudos.Pdf;
using SMSMais.Core.Laudos.Verificacao;
using SMSMais.Core.Midias;
using SMSMais.Core.Pacientes;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Laudos;

/// <summary>
/// ADR-0061 — modos de assinatura por médico e selo de verificação do laudo. Cobre o que não
/// depende de banco: o rodapé do documento oficial com o QR, o carimbo "Emitido em" do médico
/// sem certificado e a leitura tolerante das respostas da IntegraICP. Com
/// <c>DUMP_LAUDO_SAMPLES=1</c> os PDFs vão para <c>%TEMP%/smsmarica-laudo-samples</c>.
/// </summary>
public class AssinaturaModosTests
{
    static AssinaturaModosTests() => QuestPDF.Settings.License = LicenseType.Community;

    // ---------------- Rodapé com selo ----------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pdf_oficial_leva_o_qr_do_selo_no_rodape(bool assinaturaDigital)
    {
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Finalizado));
        var semSelo = await renderer.GerarAsync(Guid.NewGuid(), ModoRodapeLaudo.PreparandoAssinatura);
        var comSelo = await renderer.GerarOficialAsync(Guid.NewGuid(), SeloDeTeste(assinaturaDigital));

        Encoding().GetString(comSelo, 0, 4).Should().Be("%PDF");
        // O QR entra como imagem no rodapé de cada página: o documento cresce.
        comSelo.Length.Should().BeGreaterThan(semSelo.Length);
        Despejar($"laudo-oficial-{(assinaturaDigital ? "digital" : "sem-certificado")}.pdf", comSelo);
    }

    [Fact]
    public async Task Rascunho_nunca_leva_selo()
    {
        // Laudo não finalizado é rebaixado a Rascunho mesmo no caminho oficial: um rascunho
        // com QR de "documento válido" seria o pior erro possível.
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Rascunho));
        var oficial = await renderer.GerarOficialAsync(Guid.NewGuid(), SeloDeTeste(true));
        var rascunho = await renderer.GerarAsync(Guid.NewGuid(), ModoRodapeLaudo.Rascunho);

        // Mesmo layout, sem a imagem do QR: tamanhos na mesma ordem de grandeza.
        Math.Abs(oficial.Length - rascunho.Length).Should().BeLessThan(SeloDeTeste(true).QrPng.Length / 2);
    }

    // ---------------- Carimbo ----------------

    [Fact]
    public void Carimbo_sem_certificado_renderiza()
    {
        var png = new CarimboAssinaturaRenderer().Renderizar(new CarimboDados(
            Rubrica: null, Formato: FormatoAssinaturaMedico.Quadrada,
            Nome: "Dra. Teste", Crm: "123", UfCrm: "RJ", Rqe: null,
            DataAssinatura: new DateTime(2026, 9, 24, 10, 0, 0), AssinaturaDigital: false));

        png.Take(4).Should().Equal(0x89, (byte)'P', (byte)'N', (byte)'G');
    }

    [Fact]
    public async Task Carimbo_sem_certificado_e_estampado_no_servidor_sem_campo_de_assinatura()
    {
        // O carimbo real (PNG 800×800 com fundo transparente) sobre o PDF oficial com selo.
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Finalizado));
        var baseOficial = await renderer.GerarOficialAsync(Guid.NewGuid(), SeloDeTeste(false));
        var carimbo = new CarimboAssinaturaRenderer().Renderizar(new CarimboDados(
            Rubrica: null, Formato: FormatoAssinaturaMedico.Quadrada,
            Nome: "Dra. Teste", Crm: "123", UfCrm: "RJ", Rqe: null,
            DataAssinatura: new DateTime(2026, 9, 24, 10, 0, 0), AssinaturaDigital: false));

        var carimbado = CarimboPdf.Estampar(baseOficial, carimbo, new CarimboPosicaoPdf(1, 232, 28, 130, 130));

        using var doc = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(carimbado), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(1);
        // Nenhum formulário/campo de assinatura: carimbado nunca se passa por assinado.
        doc.Internals.Catalog.Elements.ContainsKey("/AcroForm").Should().BeFalse();
        carimbado.Length.Should().BeGreaterThan(baseOficial.Length);
        Despejar("laudo-oficial-carimbado.pdf", carimbado);
    }

    // ---------------- IntegraICP: leitura tolerante ----------------

    [Theory]
    [InlineData("""[{"provider":"VIDAAS","url":"https://psc.exemplo/autorizar?x=1"}]""")]
    [InlineData("""{"clearances":[{"provider":"VIDAAS","authorizationUrl":"https://psc.exemplo/autorizar?x=1"}]}""")]
    [InlineData("""{"clearances":[{"psc":{"nome":"BIRDID","link":"https://outro.exemplo/a"}},{"psc":{"nome":"VALID VIDaaS","link":"https://psc.exemplo/autorizar?x=1"}}]}""")]
    public void Acha_a_url_de_autorizacao_em_formatos_diferentes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        IntegraIcpClient.AcharUrlAutorizacao(doc.RootElement).Should().Be("https://psc.exemplo/autorizar?x=1");
    }

    [Fact]
    public void Sem_autorizacao_disponivel_devolve_nulo()
    {
        using var doc = JsonDocument.Parse("""{"clearances":[],"unavailables":[{"provider":"VIDAAS","reason":"NOT_FOUND"}]}""");
        IntegraIcpClient.AcharUrlAutorizacao(doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Decodifica_pem_com_e_sem_cabecalho()
    {
        var der = new byte[] { 0x30, 0x82, 0x01, 0x0a, 0x02, 0x03 };
        var b64 = Convert.ToBase64String(der);

        IntegraIcpClient.DecodificarPem($"-----BEGIN CERTIFICATE-----\n{b64}\n-----END CERTIFICATE-----\n")
            .Should().Equal(der);
        IntegraIcpClient.DecodificarPem(b64).Should().Equal(der);
    }

    // ---------------- helpers ----------------

    private static SeloVerificacaoLaudo SeloDeTeste(bool assinaturaDigital)
    {
        var codigo = Guid.Parse("7f3c2a10-5b1e-4c8d-9a2f-0e6b4d1c3a77");
        var url = $"https://api.exemplo/publico/laudos/{codigo}";
        using var gerador = new QRCoder.QRCodeGenerator();
        using var dados = gerador.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.M);
        return new SeloVerificacaoLaudo(codigo, url, new QRCoder.PngByteQRCode(dados).GetGraphic(10), assinaturaDigital);
    }

    private static LaudoPdfRenderer MontarRenderer(Laudo laudo)
    {
        var laudosSvc = Substitute.For<ILaudosService>();
        laudosSvc.CarregarParaPdfAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(laudo);

        var cfg = Substitute.For<ILaudoConfiguracaoService>();
        cfg.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new LaudoConfiguracaoDto(string.Empty, "{}", string.Empty, "{}", false, false, 7, 3, DateTime.UtcNow));

        var instituicao = Substitute.For<IInstituicaoService>();
        instituicao.ObterAsync(Arg.Any<CancellationToken>()).Returns(IInstituicaoService.ObterPadrao());

        return new LaudoPdfRenderer(
            laudosSvc, cfg, Substitute.For<IMidiasService>(), Substitute.For<IPacientesService>(),
            Substitute.For<ISolicitacoesExameService>(), Substitute.For<IConsultaStudyClient>(),
            instituicao, Options.Create(new LaudosPdfOptions()));
    }

    private static Laudo LaudoExemplo(StatusLaudo status) => new()
    {
        Id = Guid.NewGuid(),
        Titulo = "MAMOGRAFIA DIGITAL BILATERAL",
        ConteudoHtml = "<h3>CONCLUSÃO</h3><p>Categoria 2 (BI-RADS) — achados benignos.</p>",
        Status = status,
        MedicoNomeSnapshot = "Dra. Teste",
        MedicoCrmSnapshot = "123",
        MedicoUfCrmSnapshot = "RJ",
        StudyInstanceUID = "2.25.1",
        CriadoEm = DateTime.UtcNow,
        FinalizadoEm = status == StatusLaudo.Finalizado ? DateTime.UtcNow : null,
    };

    private static System.Text.Encoding Encoding() => System.Text.Encoding.ASCII;

    private static void Despejar(string nome, byte[] bytes)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUMP_LAUDO_SAMPLES"))) return;
        var dir = Path.Combine(Path.GetTempPath(), "smsmarica-laudo-samples");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, nome), bytes);
    }
}
