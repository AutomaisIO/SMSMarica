using FluentAssertions;
using QuestPDF.Infrastructure;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Exames;
using SMSMais.Core.Regulacao.Indicadores;
using SMSMais.Core.Regulacao.Indicadores.Dtos;

namespace SMSMais.Tests.Regulacao.Indicadores;

/// <summary>
/// Indicadores de Regulação: recorte de meses, categorias de motivo de cancelamento e o PDF.
/// O cálculo em si é SQL sobre o espelho e foi conferido contra o relatório de 30/09/2026.
/// </summary>
public class IndicadoresRegulacaoTests
{
    static IndicadoresRegulacaoTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static DateOnly UltimoMesFechado()
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        return new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-1);
    }

    [Fact]
    public void Sem_periodo_sao_os_12_meses_fechados_ate_o_mes_anterior()
    {
        var p = IndicadoresRegulacaoService.Periodo(null, null);
        var ultimo = UltimoMesFechado();

        p.Meses.Should().HaveCount(12);
        p.Meses[^1].Should().Be(PeriodoIndicadores.Chave(ultimo));
        p.Fim.Should().Be(ultimo.AddMonths(1).AddDays(-1));
        p.Inicio.Should().Be(ultimo.AddMonths(-11));
    }

    [Fact]
    public void Periodo_explicito_vira_o_primeiro_e_o_ultimo_dia_dos_meses()
    {
        var p = IndicadoresRegulacaoService.Periodo(new DateOnly(2025, 1, 15), new DateOnly(2025, 12, 3));

        p.Inicio.Should().Be(new DateOnly(2025, 1, 1));
        p.Fim.Should().Be(new DateOnly(2025, 12, 31));
        p.Meses.Should().HaveCount(12).And.StartWith("2025-01").And.EndWith("2025-12");
        p.Anos.Should().Equal("2025");
    }

    [Fact]
    public void Mes_em_aberto_nao_entra()
    {
        var atual = UltimoMesFechado().AddMonths(1);
        var acao = () => IndicadoresRegulacaoService.Periodo(atual.AddMonths(-2), atual);
        acao.Should().Throw<ValidacaoException>();
    }

    [Fact]
    public void Periodo_passa_de_24_meses_ou_invertido_ou_pela_metade_e_recusado()
    {
        var fim = UltimoMesFechado();
        FluentActions.Invoking(() => IndicadoresRegulacaoService.Periodo(fim.AddMonths(-24), fim)).Should().Throw<ValidacaoException>();
        FluentActions.Invoking(() => IndicadoresRegulacaoService.Periodo(fim, fim.AddMonths(-1))).Should().Throw<ValidacaoException>();
        FluentActions.Invoking(() => IndicadoresRegulacaoService.Periodo(fim, null)).Should().Throw<ValidacaoException>();
        IndicadoresRegulacaoService.Periodo(fim.AddMonths(-23), fim).Meses.Should().HaveCount(24);
    }

    [Theory]
    [InlineData("PACIENTE FALECEU EM 03/04", "Óbito")]
    [InlineData("Óbito", "Óbito")]
    [InlineData("CANCELAR", MotivoCancelamento.SemMotivo)]
    [InlineData(" . ", MotivoCancelamento.SemMotivo)]
    [InlineData("paciente desistiu, fez particular", "Desistência / impedimento do paciente")]
    [InlineData("Diversas tentativas sem sucesso de contato", "Sem contato com o paciente")]
    [InlineData("xpto abc", MotivoCancelamento.Outros)]
    public void Motivo_vira_categoria(string texto, string categoria) =>
        MotivoCancelamento.Categoria(texto).Should().Be(categoria);

    [Fact]
    public void Cancelamento_generico_usa_o_ultimo_followup()
    {
        MotivoCancelamento.CategoriaComFollowUp("Não respondida no prazo estabelecido", "FalhaContato", "liguei 3 vezes")
            .Should().Be("Sem contato com o paciente");
        // Texto do cancelamento que já diz o porquê não é trocado pelo FollowUP.
        MotivoCancelamento.CategoriaComFollowUp("Paciente faleceu", "FalhaContato", null).Should().Be("Óbito");
        // Contato realizado com observação vaga: fica o texto do cancelamento.
        MotivoCancelamento.CategoriaComFollowUp("Não respondida no prazo", "ContatoRealizado", "ok")
            .Should().Be("Sem resposta no prazo");
    }

    [Fact]
    public void Pdf_sai_com_todas_as_formas_de_secao_e_24_meses()
    {
        var meses = Enumerable.Range(0, 24).Select(i => PeriodoIndicadores.Chave(new DateOnly(2024, 9, 1).AddMonths(i))).ToList();
        Dictionary<string, decimal?> Serie(Func<int, decimal?> v) => meses.Select((m, i) => (m, i)).ToDictionary(x => x.m, x => v(x.i));
        var textoLongo = string.Concat(Enumerable.Repeat("Ambulatório 1ª vez - Cirurgia de Cabeça e Pescoço (Oncologia) ", 3));
        var dto = new IndicadoresRegulacaoDto(FonteIndicadorRegulacao.Ser, "SER", "SER — Sistema Estadual de Regulação", meses,
            new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc), ["Espelho da fila.", "Outra ressalva."],
            [
                new SecaoIndicadorDto("vagas", "Vagas", "Texto.", false,
                [
                    new SerieIndicadorDto("Ofertadas", SeloIndicador.Indisponivel, "Não fornece.", FormatoIndicador.Inteiro, false, false,
                        Serie(_ => null), new Dictionary<string, decimal?>(), AgregacaoIndicador.Soma),
                    new SerieIndicadorDto("Utilizadas", SeloIndicador.Oficial, null, FormatoIndicador.Inteiro, true, false,
                        Serie(i => 1000 + i * 37), new Dictionary<string, decimal?>(), AgregacaoIndicador.Soma),
                    new SerieIndicadorDto("Consultas", SeloIndicador.Oficial, null, FormatoIndicador.Inteiro, false, true,
                        Serie(i => 600 + i), new Dictionary<string, decimal?>(), AgregacaoIndicador.Soma),
                    new SerieIndicadorDto("Exames", SeloIndicador.Oficial, null, FormatoIndicador.Inteiro, false, true,
                        Serie(i => 400 + i), new Dictionary<string, decimal?>(), AgregacaoIndicador.Soma),
                    new SerieIndicadorDto("Absenteísmo", SeloIndicador.Calculado, null, FormatoIndicador.Percentual, false, false,
                        Serie(i => 20.5m + i % 5), new Dictionary<string, decimal?> { ["2025"] = 22.3m }, AgregacaoIndicador.Media),
                    new SerieIndicadorDto("Fila no fim do mês", SeloIndicador.Parcial, "Piso.", FormatoIndicador.Inteiro, false, false,
                        Serie(i => 75000 - i * 100), new Dictionary<string, decimal?>(), AgregacaoIndicador.UltimoMes),
                    new SerieIndicadorDto("Espera — mediana (dias)", SeloIndicador.Calculado, null, FormatoIndicador.Dias, false, false,
                        Serie(i => i % 3 == 0 ? null : 12.5m), new Dictionary<string, decimal?>(), AgregacaoIndicador.Media),
                ],
                new GraficoIndicadorDto("empilhado", ["Consultas", "Exames"]),
                new TabelaIndicadorDto("Motivos", ["Motivo", "2025", "2026", "Total", "% do total"],
                    [["Outros", "10", "5", "15", "10,0%"], ["Óbito", "50", "40", "90", "60,0%"], ["Sem motivo informado", "30", "15", "45", "30,0%"]], "Categorias."),
                [
                    new TabelaIndicadorDto("Judicializadas", ["ID", "Tipo", "Recurso", "Entrada", "Situação", "Dias até agendar"],
                        Enumerable.Range(0, 80).Select(i => (IReadOnlyList<string?>)[$"{7000000 + i}", "Consultas", textoLongo, "01/02/2026", "Chegada confirmada", i % 4 == 0 ? "—" : $"{i}"]).ToList(),
                        null),
                ],
                new ResumoTempoDto(12, 30, 90, 0, 400)),
                new SecaoIndicadorDto("absenteismo", "Absenteísmo", "O sistema não informa.", true, [], null, null, [], null),
                new SecaoIndicadorDto("fila", "Fila", null, false,
                    [new SerieIndicadorDto("Tudo zero", SeloIndicador.Oficial, null, FormatoIndicador.Inteiro, false, false,
                        Serie(_ => 0), new Dictionary<string, decimal?>(), AgregacaoIndicador.Soma)],
                    new GraficoIndicadorDto("barras", ["Tudo zero"]), null, [], null),
            ]);
        var idv = new IdentidadeVisualPdf("Secretaria de Saúde", "#C8102E", "#7C0A1C", null);

        var pdf = new IndicadoresRegulacaoPdf(dto, idv).Gerar();

        pdf.Length.Should().BeGreaterThan(10_000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    /// <summary>
    /// A amostra do SISREG tem ~o mesmo tanto de linhas em todo mês. Juntar o ano daria o mesmo peso a um mês
    /// de 2.400 e a um de 1.200 cancelamentos; cada mês tem de pesar pelo seu total oficial.
    /// </summary>
    [Fact]
    public void Motivos_por_amostra_pesam_cada_mes_pelo_seu_total()
    {
        var linhas = Enumerable.Repeat(("2025-01", "Óbito"), 50)
            .Concat(Enumerable.Repeat(("2025-01", "Outros"), 50))
            .Concat(Enumerable.Repeat(("2025-02", "Óbito"), 100))
            .ToList();
        var total = new Dictionary<string, int> { ["2025-01"] = 2400, ["2025-02"] = 1200 };

        var e = IndicadoresSisregCalculo.EstimarMotivos(linhas, total);

        e.Amostra.Should().BeTrue();
        // Óbito: 50×24 + 100×12 = 2.400 de 3.600 estimados → 66,7% (juntando o ano seria 75%).
        e.Percentual[(2025, "Óbito")].Should().Be(66.7m);
        e.Percentual[(2025, "Outros")].Should().Be(33.3m);
        e.TotalAno[2025].Should().Be(3600);
        e.Categorias.Should().StartWith("Óbito");
    }

    [Fact]
    public void Motivos_lidos_inteiros_nao_sao_amostra()
    {
        var linhas = new[] { ("2026-09", "Óbito"), ("2026-09", "Duplicidade") };
        var e = IndicadoresSisregCalculo.EstimarMotivos(linhas, new Dictionary<string, int> { ["2026-09"] = 2 });

        e.Amostra.Should().BeFalse();
        e.Lidas[(2026, "Óbito")].Should().Be(1);
    }
}
