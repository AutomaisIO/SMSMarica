using Microsoft.Extensions.Options;
using NSubstitute;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Laudos.Configuracao.Dtos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.Medicos.Assinatura;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using QuestDocument = QuestPDF.Fluent.Document;

namespace SMSMarica.Tests.Laudos;

/// <summary>
/// Cobre os 3 estados de rodapé/marca d'água do PDF do laudo e o carimbo da
/// assinatura. Defina a variável de ambiente <c>DUMP_LAUDO_SAMPLES=1</c> para
/// despejar PDFs/PNGs de amostra em <c>%TEMP%/smsmarica-laudo-samples</c> e
/// inspecioná-los visualmente.
/// </summary>
public class LaudoPdfRodapeTests
{
    static LaudoPdfRodapeTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static bool DumpAtivo =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUMP_LAUDO_SAMPLES"));

    private static string DumpDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "smsmarica-laudo-samples");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static LaudoPdfRenderer MontarRenderer(Laudo laudo)
    {
        var laudosSvc = Substitute.For<ILaudosService>();
        laudosSvc.CarregarParaPdfAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(laudo);

        var cfg = Substitute.For<ILaudoConfiguracaoService>();
        cfg.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new LaudoConfiguracaoDto(string.Empty, "{}", string.Empty, "{}", false, false, 7, 7, DateTime.UtcNow));

        var midias = Substitute.For<IMidiasService>();
        var pacientes = Substitute.For<IPacientesService>();
        var solicitacoes = Substitute.For<ISolicitacoesExameService>();

        return new LaudoPdfRenderer(laudosSvc, cfg, midias, pacientes, solicitacoes, Options.Create(new LaudosPdfOptions()));
    }

    private static Laudo LaudoExemplo(StatusLaudo status) => new()
    {
        Id = Guid.NewGuid(),
        Titulo = "MAMOGRAFIA DIGITAL BILATERAL",
        ConteudoHtml = "<h3>INDICAÇÃO</h3><p>Exame de rastreamento.</p>" +
                       "<h3>AVALIAÇÃO</h3><p>Mamas com predomínio fibroglandular. " +
                       "Sem nódulos, distorções ou microcalcificações suspeitas.</p>" +
                       "<h3>CONCLUSÃO</h3><p>Categoria 2 (BI-RADS) — achados benignos.</p>",
        Status = status,
        MedicoNomeSnapshot = "Dra. Claudia Freixo Seixas",
        MedicoCrmSnapshot = "52702650",
        MedicoUfCrmSnapshot = "RJ",
        MedicoRqeSnapshot = "12345",
        StudyInstanceUID = "2.25.987654321",
        CriadoEm = DateTime.UtcNow,
        FinalizadoEm = status == StatusLaudo.Finalizado ? DateTime.UtcNow : null,
    };

    [Fact]
    public async Task Rascunho_gera_pdf_com_marca_dagua()
    {
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Rascunho));
        // Mesmo pedindo o modo "finalizado não assinado", laudo não-finalizado é
        // rebaixado para Rascunho (marca d'água) pelo renderer.
        var pdf = await renderer.GerarAsync(Guid.NewGuid(), ModoRodapeLaudo.FinalizadoNaoAssinado);

        EhPdf(pdf).Should().BeTrue();
        Despejar("laudo-1-rascunho.pdf", pdf);
    }

    [Fact]
    public async Task Finalizado_nao_assinado_gera_pdf_com_tarja()
    {
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Finalizado));
        var pdf = await renderer.GerarAsync(Guid.NewGuid(), ModoRodapeLaudo.FinalizadoNaoAssinado);

        EhPdf(pdf).Should().BeTrue();
        Despejar("laudo-2-finalizado-nao-assinado.pdf", pdf);
    }

    [Fact]
    public async Task Preparando_assinatura_gera_pdf_base_limpo()
    {
        var renderer = MontarRenderer(LaudoExemplo(StatusLaudo.Finalizado));
        var pdf = await renderer.GerarAsync(Guid.NewGuid(), ModoRodapeLaudo.PreparandoAssinatura);

        EhPdf(pdf).Should().BeTrue();
        Despejar("laudo-3-preparando-assinatura.pdf", pdf);
    }

    [Theory]
    [InlineData(FormatoAssinaturaMedico.Quadrada, 800, 800)]
    [InlineData(FormatoAssinaturaMedico.Horizontal, 800, 400)]
    public void Carimbo_com_rubrica_opaca_poe_texto_por_cima(FormatoAssinaturaMedico formato, int w, int h)
    {
        // Rubrica de teste com FUNDO OPACO — se o z-order estiver errado, o nome
        // do médico ficaria escondido atrás dela.
        var rubrica = RubricaFakeOpaca(w, h);

        var carimbo = new CarimboAssinaturaRenderer().Renderizar(new CarimboDados(
            Rubrica: rubrica, Formato: formato,
            Nome: "Dra. Claudia Freixo Seixas", Crm: "52702650", UfCrm: "RJ", Rqe: "12345"));

        DimensaoImagem.Ler(carimbo).Should().Be((800, 800)); // quadrado virtual fixo
        Despejar($"carimbo-{formato}.png", carimbo);
        Despejar($"rubrica-fake-{formato}.png", rubrica);
    }

    // ---- helpers ----

    private static bool EhPdf(byte[] b) =>
        b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46; // %PDF

    private static void Despejar(string nome, byte[] bytes)
    {
        if (DumpAtivo) File.WriteAllBytes(Path.Combine(DumpDir, nome), bytes);
    }

    /// <summary>Gera um PNG WxH com fundo OPACO + um traço simulando assinatura.</summary>
    private static byte[] RubricaFakeOpaca(int w, int h) =>
        QuestDocument.Create(c => c.Page(p =>
        {
            p.Size(w, h, Unit.Point);
            p.Margin(0);
            p.PageColor("#CFE3F7"); // azul claro OPACO (preenche todo o frame)
            p.Content().AlignMiddle().AlignCenter()
                .Text("assinatura").FontSize(44).Italic().FontColor("#16324F");
        }))
        .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 72 })
        .First();
}
