using Microsoft.Extensions.Options;
using NSubstitute;
using QuestPDF.Infrastructure;
using SMSMais.Core.Institucional;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Configuracao;
using SMSMais.Core.Laudos.Configuracao.Dtos;
using SMSMais.Core.Laudos.Pdf;
using SMSMais.Core.Midias;
using SMSMais.Core.Pacientes;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Laudos;

/// <summary>
/// Tabela de DADOS no corpo do laudo (<c>&lt;table class="laudo-tabela"&gt;</c>):
/// grade contínua, cabeçalho repetido e larguras de coluna. O caso-guia é a
/// densitometria óssea — 7 colunas, que é o pior caso de largura numa A4.
///
/// <para>
/// O 2º teste é a trava de regressão do <b>cabeçalho institucional</b>: tabela
/// SEM a classe continua no caminho legado (uma caixa por &lt;tr&gt;).
/// </para>
/// </summary>
public class LaudoPdfTabelaDadosTests
{
    private const string TabelaDensitometria = """
        <table class="laudo-tabela"><tbody>
        <tr><th style="width:17%"><p>Região estudada</p></th><th style="width:14%"><p>Sítio</p></th><th style="width:14%"><p>DMO (g/cm²)</p></th><th style="width:12%"><p>T-Score</p></th><th style="width:11%"><p>PR (%)</p></th><th style="width:12%"><p>Z-Score</p></th><th style="width:11%"><p>AM (%)</p></th></tr>
        <tr><td><p>Coluna Lombar</p></td><td><p>L1 a L4</p></td><td><p>0,982</p></td><td><p>-1,4</p></td><td><p>87</p></td><td><p>-0,8</p></td><td><p>92</p></td></tr>
        <tr><td><p>Fêmur Proximal</p></td><td><p>Fêmur Total</p></td><td><p>0,845</p></td><td><p>-1,9</p></td><td><p>81</p></td><td><p>-1,1</p></td><td><p>89</p></td></tr>
        <tr><td><p>Fêmur Proximal</p></td><td><p>Colo Femoral</p></td><td><p>0,791</p></td><td><p>-2,6</p></td><td><p>74</p></td><td><p>-1,5</p></td><td><p>85</p></td></tr>
        <tr><td><p>Antebraço</p></td><td><p>Rádio 33%</p></td><td><p>0,612</p></td><td><p>-2,1</p></td><td><p>79</p></td><td><p>-1,3</p></td><td><p>87</p></td></tr>
        </tbody></table>
        """;

    [Fact]
    public async Task Tabela_de_dados_de_7_colunas_cabe_na_pagina_e_gera_pdf()
    {
        var html = $"""
            <h3>RELATÓRIO DA ANÁLISE RESUMIDO</h3>
            {TabelaDensitometria}
            <h3>DIAGNÓSTICO</h3>
            <p>Segundo os critérios da OMS e posições oficiais da SBDens / ISCD:</p>
            <p>A paciente apresenta OSTEOPOROSE.</p>
            """;

        var pdf = await GerarAsync(html, cabecalhoHtml: string.Empty);

        pdf.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Largura_de_coluna_sobrevive_ao_round_trip_pelo_editor_via_colwidth()
    {
        // Depois de abrir e salvar o laudo no TipTap, o style="width:%" some das
        // células (não é atributo do nó) e sobra o colwidth em px. A tabela tem
        // que sair com as MESMAS proporções nos dois formatos.
        var html = """
            <table class="laudo-tabela"><colgroup><col style="width: 119px"><col style="width: 98px"></colgroup><tbody>
            <tr><th colwidth="119"><p>Região estudada</p></th><th colwidth="98"><p>Sítio</p></th></tr>
            <tr><td><p>Coluna Lombar</p></td><td><p>L1 a L4</p></td></tr>
            </tbody></table>
            """;

        var pdf = await GerarAsync(html, cabecalhoHtml: string.Empty);

        pdf.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Tabela_com_muitas_linhas_repete_o_cabecalho_sem_estourar_o_layout()
    {
        // 60 linhas força quebra de página: exercita o Header() repetido. QuestPDF
        // lança em overflow de layout, então "gerou" já é a asserção forte aqui.
        var linhas = string.Concat(Enumerable.Range(1, 60).Select(i =>
            $"<tr><td><p>Região {i}</p></td><td><p>Sítio {i}</p></td><td><p>0,9{i:00}</p></td>"
            + "<td><p>-1,5</p></td><td><p>85</p></td><td><p>-0,9</p></td><td><p>91</p></td></tr>"));

        var html = "<table class=\"laudo-tabela\"><tbody>"
            + "<tr><th><p>Região estudada</p></th><th><p>Sítio</p></th><th><p>DMO (g/cm²)</p></th>"
            + "<th><p>T-Score</p></th><th><p>PR (%)</p></th><th><p>Z-Score</p></th><th><p>AM (%)</p></th></tr>"
            + linhas + "</tbody></table>";

        var pdf = await GerarAsync(html, cabecalhoHtml: string.Empty);

        pdf.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Linha_com_menos_celulas_nao_desloca_a_tabela()
    {
        // HTML editado na mão pode ter linha curta. O QuestPDF posiciona célula a
        // célula, então sem preenchimento a 1ª célula da linha seguinte subiria
        // para o buraco e deslocaria tudo dali para baixo — sem erro nenhum.
        var html = """
            <table class="laudo-tabela"><tbody>
            <tr><th><p>A</p></th><th><p>B</p></th><th><p>C</p></th></tr>
            <tr><td><p>a1</p></td><td><p>b1</p></td></tr>
            <tr><td><p>a2</p></td><td><p>b2</p></td><td><p>c2</p></td></tr>
            </tbody></table>
            """;

        var pdf = await GerarAsync(html, cabecalhoHtml: string.Empty);

        pdf.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Tabela_sem_a_classe_continua_no_caminho_legado_de_layout()
    {
        // Regressão do cabeçalho institucional: logo|texto|logo é uma tabela de
        // LAYOUT (sem classe) e precisa continuar caindo em RenderLinha.
        var cabecalho = "<table><tr><td><p><strong>SECRETARIA MUNICIPAL DE SAÚDE</strong></p></td>"
            + "<td><p>CDT - CENTRO DE DIAGNÓSTICO E TRATAMENTO</p></td></tr></table>";

        var pdf = await GerarAsync("<p>Corpo do laudo.</p>", cabecalhoHtml: cabecalho);

        pdf.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    private static async Task<byte[]> GerarAsync(string conteudoHtml, string cabecalhoHtml)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var laudo = new Laudo
        {
            Id = Guid.NewGuid(),
            Titulo = "DENSITOMETRIA ÓSSEA",
            ConteudoHtml = conteudoHtml,
            Status = StatusLaudo.Finalizado,
            MedicoNomeSnapshot = "Dra. Claudia Freixo Seixas",
            MedicoCrmSnapshot = "52702650",
            MedicoUfCrmSnapshot = "RJ",
            StudyInstanceUID = "2.25.123",
            CriadoEm = DateTime.UtcNow,
            FinalizadoEm = DateTime.UtcNow,
        };

        var laudosSvc = Substitute.For<ILaudosService>();
        laudosSvc.CarregarParaPdfAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(laudo);

        var cfg = Substitute.For<ILaudoConfiguracaoService>();
        cfg.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(new LaudoConfiguracaoDto(cabecalhoHtml, "{}", string.Empty, "{}", false, false, 7, 3, DateTime.UtcNow));

        var instituicao = Substitute.For<IInstituicaoService>();
        instituicao.ObterAsync(Arg.Any<CancellationToken>()).Returns(IInstituicaoService.ObterPadrao());

        var renderer = new LaudoPdfRenderer(
            laudosSvc,
            cfg,
            Substitute.For<IMidiasService>(),
            Substitute.For<IPacientesService>(),
            Substitute.For<ISolicitacoesExameService>(),
            Substitute.For<IConsultaStudyClient>(),
            instituicao,
            Options.Create(new LaudosPdfOptions()));

        var pdf = await renderer.GerarAsync(laudo.Id);

        // Conferência visual sob demanda: LAUDO_PDF_DUMP=<pasta> salva o PDF gerado.
        var pasta = Environment.GetEnvironmentVariable("LAUDO_PDF_DUMP");
        if (!string.IsNullOrWhiteSpace(pasta) && Directory.Exists(pasta))
        {
            await File.WriteAllBytesAsync(Path.Combine(pasta, $"laudo-{laudo.Id:N}.pdf"), pdf);
        }

        return pdf;
    }
}
