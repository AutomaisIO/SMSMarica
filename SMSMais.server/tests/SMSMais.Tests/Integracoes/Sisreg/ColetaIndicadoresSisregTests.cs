using FluentAssertions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Coletor dos Indicadores de Regulação do SISREG, sem banco e sem SISREG: as telas são HTML
/// SINTÉTICO no formato medido em 30/09/2026 (nenhum dado real). O que se protege aqui é o que custa
/// caro quando erra: gravar leitura incompleta, apagar dado bom com resposta truncada e bater no
/// SISREG na hora errada.
/// </summary>
public class ColetaIndicadoresSisregTests
{
    // ================================================================= HTML sintético das telas

    private static string Faltas(int? paginas, int linhas, bool nenhum = false, bool fechar = true)
    {
        var corpo = string.Concat(Enumerable.Range(0, linhas).Select(i =>
            $"<TR><td align='center'>{700000000 + i}</td><td>2266741 - UNIDADE TESTE</td><td>PACIENTE</td>"
            + "<td>RUA</td><td>(21) 0000-0000</td><td>15/01/2031</td><td>08:00</td><td>0301010072 - CONSULTA</td></TR>"));
        return "<html><body><table><tr><td class='td_titulo_tabela'>Cod. Solicitacao</td><td>Unidade Solicitante</td>"
               + "<td>Data de Execucao</td></tr>" + corpo + "</table>"
               + (paginas is { } p ? $"Mostrando P&aacute;gina <input name='txtPagina' value='1'> de {p}" : "")
               + (nenhum ? "<p>Nenhum registro encontrado.</p>" : "")
               + (fechar ? "</body></html>" : "");
    }

    private static string Canceladas(int declaradas, int paginas, params string[] codigos) =>
        $"<html><body><table><TR><td class='td_titulo_tabela'>MARCA&Ccedil;&Otilde;ES PESQUISADAS ({declaradas})</TD></TR>"
        + string.Concat(codigos.Select(c =>
            $"<tr><td>{c}</td><td>23/10/2031</td><td>08:00:00</td><td>CONSULTA EM CARDIOLOGIA</td><td>DR X</td>"
            + "<td>PACIENTE</td><td>paciente desistiu</td><td>123OPERADOR</td><td>18.09.2031 11:24:54</td></tr>"))
        + $"</table><a onClick=\"exibirPagina(1,{paginas}); return false;\">Pr&oacute;xima</a></body></html>";

    private static string Gerenciador(int? declaradas, int linhas) =>
        "<html><body><table>"
        + (declaradas is { } d ? $"<td class='td_titulo_tabela'>SOLICITA&Ccedil;&Otilde;ES RETORNADAS ({d})</TD>" : "")
        + string.Concat(Enumerable.Range(0, linhas).Select(i =>
            $"<tr><td>{800000000 + i}</td><td>02/03/2031</td><td></td><td>PACIENTE</td><td>(21) 0</td><td>M</td>"
            + "<td>40 anos</td><td>CONSULTA - CARDIOLOGIA</td><td>R99</td><td>UNIDADE</td><td>---</td><td>SOL/DEV/REG</td></tr>"))
        + "</table></body></html>";

    private static string Ppi(params (string Interno, int Total, int Usada, string Saldo)[] cotas) =>
        "<html><body><TABLE><TR><TD colspan='8' class=\"td_titulo_tabela\">PPI por Procedimento:</TD></TR>"
        + "<TR><TD>Cod. Unificado</TD><TD>Cod. Interno</TD><TD>Procedimento</TD><TD>PPI<BR>Total</TD></TR>"
        + string.Concat(cotas.Select(c =>
            $"<TR class=\"par_tr\"><TD>0302020020</TD><TD>{c.Interno}</TD><TD>ATENDIMENTO</TD><TD>{c.Total}</TD>"
            + $"<TD>{c.Usada}</TD><TD>{c.Saldo}</TD><TD>FISICO</TD><TD onClick=\"detalharCotaUnidade('{c.Interno}');\">Detalhar</TD></TR>"))
        + "</TABLE></body></html>";

    // ============================================================================ parsers

    [Fact]
    public void Faltas_le_so_as_colunas_que_nao_identificam_o_paciente()
    {
        var html = Faltas(paginas: 1, linhas: 2);

        IndicadoresSisregHtmlParser.TelaDeFaltas(html).Should().BeTrue();
        IndicadoresSisregHtmlParser.PaginasDeFaltas(html).Should().Be(1);
        var f = IndicadoresSisregHtmlParser.Faltas(html);
        f.Should().HaveCount(2);
        f[0].Should().Be(new FaltaLidaSisreg("700000000", "2266741 - UNIDADE TESTE", new DateOnly(2031, 1, 15), "08:00", "0301010072 - CONSULTA"));
    }

    [Fact]
    public void Canceladas_leem_total_declarado_paginas_e_o_instante_em_UTC()
    {
        var html = Canceladas(41, 3, "123456789");

        IndicadoresSisregHtmlParser.TotalCanceladas(html).Should().Be((41, 3));
        var c = IndicadoresSisregHtmlParser.Canceladas(html).Single();
        c.Codigo.Should().Be("123456789");
        c.DataMarcacao.Should().Be(new DateOnly(2031, 10, 23));
        c.Justificativa.Should().Be("paciente desistiu");
        c.CanceladoEm.Should().Be(new DateTime(2031, 9, 18, 14, 24, 54, DateTimeKind.Utc)); // Brasília +3
    }

    [Fact]
    public void Ppi_aceita_saldo_negativo_e_ignora_o_cabecalho()
    {
        var html = Ppi(("1801026", 1000, 1002, "-2"), ("2018220", 10, 2, "8"));

        IndicadoresSisregHtmlParser.TelaDePpi(html).Should().BeTrue();
        var cotas = IndicadoresSisregHtmlParser.Ppi(html);
        cotas.Should().HaveCount(2);
        cotas[0].Should().Be(new CotaPpiLidaSisreg("0302020020", "1801026", "ATENDIMENTO", 1000, 1002, -2, "FISICO"));
    }

    [Fact]
    public void Gerenciador_le_retornadas_e_as_colunas_de_desfecho()
    {
        var html = Gerenciador(declaradas: 2, linhas: 2);

        IndicadoresSisregHtmlParser.Retornadas(html).Should().Be(2);
        var d = IndicadoresSisregHtmlParser.Desfechos(html);
        d.Should().HaveCount(2);
        d[0].Should().Be(new DesfechoLidoSisreg("800000000", new DateOnly(2031, 3, 2), "CONSULTA - CARDIOLOGIA", "SOL/DEV/REG"));
    }

    [Fact]
    public void Unidades_solicitantes_saem_do_select_sem_a_opcao_Todas()
    {
        const string html = "<html><select name=\"unidade_adm\"><option value=\"\">Todas</option>"
                            + "<option value=\"2266741\">AMBULATORIO TESTE</option><option value='9118268'>CAPS TESTE</option></select></html>";

        IndicadoresSisregHtmlParser.UnidadesSolicitantes(html).Should().Equal(
            new UnidadeSolicitanteSisreg("2266741", "AMBULATORIO TESTE"), new UnidadeSolicitanteSisreg("9118268", "CAPS TESTE"));
    }

    [Fact]
    public void Resposta_cortada_ou_de_gateway_nao_e_inteira()
    {
        IndicadoresSisregHtmlParser.Inteira(Faltas(1, 3, fechar: false)).Should().BeFalse();
        const string gateway = "<html><head><title>504 Gateway Time-out</title></head><body>504</body></html>";
        IndicadoresSisregHtmlParser.PaginaDeGateway(gateway).Should().BeTrue();
        IndicadoresSisregHtmlParser.Inteira(gateway).Should().BeFalse();
        // "504" como número numa página normal não é gateway.
        IndicadoresSisregHtmlParser.PaginaDeGateway(Faltas(504, 3)).Should().BeFalse();
        IndicadoresSisregHtmlParser.NenhumRegistro(Faltas(null, 0, nenhum: true)).Should().BeTrue();
    }

    // ================================================================ guarda de substituição

    [Theory]
    [InlineData(0, 0, true, null)]
    [InlineData(100, 95, true, null)]
    [InlineData(10, 2, true, null)]      // janela pequena: a trava de encolhimento não se aplica
    [InlineData(100, 50, true, "encolheu")]
    [InlineData(100, 0, true, "vazia")]
    [InlineData(0, 30, false, "provou")]
    public void Substituicao_so_com_prova_e_sem_encolher_demais(int anteriores, int novas, bool prova, string? recusa)
    {
        var motivo = SubstituicaoDeJanela.Recusar(anteriores, novas, prova, 0.7);
        if (recusa is null) motivo.Should().BeNull();
        else motivo.Should().Contain(recusa);
    }

    // ========================================================================== portão

    private static EntradaPortaoColeta Entrada(
        string hora = "10:00", bool ativa = true, bool chave = true, DateTime? pausa = null,
        bool outro = false, int gastas = 0, int restante = 400, DateTime? login = null) =>
        new(ativa, chave, pausa, new DateTime(2031, 3, 10, 13, 0, 0, DateTimeKind.Utc),
            TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture), outro, gastas, restante, login);

    [Theory]
    [InlineData("01:19", EsperaColetaIndicadores.ForaDoHorario)]
    [InlineData("18:00", EsperaColetaIndicadores.ForaDoHorario)]
    [InlineData("23:30", EsperaColetaIndicadores.ForaDoHorario)]
    [InlineData("01:20", null)]
    [InlineData("17:59", null)]
    public void Portao_respeita_a_faixa_da_varredura_das_agendas(string hora, EsperaColetaIndicadores? esperado) =>
        PortaoColetaIndicadores.Decidir(Entrada(hora), new ColetaIndicadoresOpcoes()).Should().Be(esperado);

    [Fact]
    public void Portao_nasce_desligado_cede_a_vez_e_para_no_teto_e_no_captcha()
    {
        var o = new ColetaIndicadoresOpcoes();
        PortaoColetaIndicadores.Decidir(Entrada(ativa: false), o).Should().Be(EsperaColetaIndicadores.Desligada);
        PortaoColetaIndicadores.Decidir(Entrada(chave: false), o).Should().Be(EsperaColetaIndicadores.ChaveMestraDesligada);
        PortaoColetaIndicadores.Decidir(Entrada(pausa: new DateTime(2031, 3, 11, 0, 0, 0, DateTimeKind.Utc)), o)
            .Should().Be(EsperaColetaIndicadores.PausadaPorCaptcha);
        PortaoColetaIndicadores.Decidir(Entrada(pausa: new DateTime(2031, 3, 10, 12, 0, 0, DateTimeKind.Utc)), o)
            .Should().BeNull("a pausa já passou");
        PortaoColetaIndicadores.Decidir(Entrada(outro: true), o).Should().Be(EsperaColetaIndicadores.OutroMotorUsandoASessao);
        PortaoColetaIndicadores.Decidir(Entrada(gastas: 150), o).Should().Be(EsperaColetaIndicadores.TetoDoColetor);
        PortaoColetaIndicadores.Decidir(Entrada(gastas: 149), o).Should().BeNull();
        PortaoColetaIndicadores.Decidir(Entrada(restante: 119), o).Should().Be(EsperaColetaIndicadores.OrcamentoGlobalCurto);
    }

    [Fact]
    public void Portao_espera_alguns_minutos_depois_de_login_recusado()
    {
        var o = new ColetaIndicadoresOpcoes();
        PortaoColetaIndicadores.Decidir(Entrada(login: new DateTime(2031, 3, 10, 13, 10, 0, DateTimeKind.Utc)), o)
            .Should().Be(EsperaColetaIndicadores.LoginRecusado);
        PortaoColetaIndicadores.Decidir(Entrada(login: new DateTime(2031, 3, 10, 12, 59, 0, DateTimeKind.Utc)), o)
            .Should().BeNull("a espera já passou");
    }

    [Fact]
    public void Contador_do_coletor_e_uma_janela_rolante_de_uma_hora()
    {
        var c = new ContadorRequisicoesColeta();
        var t0 = new DateTime(2031, 3, 10, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++) c.Registrar(t0.AddMinutes(i));

        c.NaUltimaHora(t0.AddMinutes(30)).Should().Be(5);
        c.NaUltimaHora(t0.AddMinutes(62)).Should().Be(2);
        c.NaUltimaHora(t0.AddHours(2)).Should().Be(0);
    }

    // ============================================================================ plano

    [Fact]
    public void Semanas_de_faltas_seguem_os_cortes_da_carga_e_so_depois_de_velhas()
    {
        var semanas = PlanoColetaIndicadores.SemanasDeFaltas(new DateOnly(2031, 3, 20), mesesRecentes: 1, diasParaFaltas: 30);

        // Limite = 18/02: fevereiro até o corte 9–16; nada de março.
        semanas.Should().Equal(
            new JanelaColeta(new DateOnly(2031, 2, 1), new DateOnly(2031, 2, 8)),
            new JanelaColeta(new DateOnly(2031, 2, 9), new DateOnly(2031, 2, 16)));
    }

    [Fact]
    public void Semanas_recentes_de_faltas_sao_o_complemento_das_oficiais_e_param_em_ontem()
    {
        var hoje = new DateOnly(2031, 3, 20);

        var recentes = PlanoColetaIndicadores.SemanasRecentesDeFaltas(hoje, diasParaFaltas: 30);

        // Limite = 18/02: o corte 17–23/02 ainda é novo; a semana corrente (17–23/03) vem cortada em ontem.
        recentes.Should().Equal(
            new JanelaColeta(new DateOnly(2031, 2, 17), new DateOnly(2031, 2, 23)),
            new JanelaColeta(new DateOnly(2031, 2, 24), new DateOnly(2031, 2, 28)),
            new JanelaColeta(new DateOnly(2031, 3, 1), new DateOnly(2031, 3, 8)),
            new JanelaColeta(new DateOnly(2031, 3, 9), new DateOnly(2031, 3, 16)),
            new JanelaColeta(new DateOnly(2031, 3, 17), new DateOnly(2031, 3, 19)));

        // Nenhuma semana nos dois planos ao mesmo tempo: a oficial começa onde a recente termina.
        var oficiais = PlanoColetaIndicadores.SemanasDeFaltas(hoje, mesesRecentes: 3, diasParaFaltas: 30);
        oficiais.Select(o => o.Inicio).Should().NotIntersectWith(recentes.Select(r => r.Inicio));
        oficiais.Max(o => o.Fim).AddDays(1).Should().Be(recentes[0].Inicio);

        // No dia 1º não existe "ontem" no mês corrente: nada do mês novo entra.
        PlanoColetaIndicadores.SemanasRecentesDeFaltas(new DateOnly(2031, 4, 1), 30)
            .Should().OnlyContain(j => j.Fim <= new DateOnly(2031, 3, 31));
    }

    [Fact]
    public void Ppi_da_competencia_so_depois_do_dia_de_corte_do_mes_seguinte()
    {
        PlanoColetaIndicadores.CompetenciasDePpi(new DateOnly(2031, 3, 4), 2, 5)
            .Should().Equal(new DateOnly(2031, 1, 1));
        PlanoColetaIndicadores.CompetenciasDePpi(new DateOnly(2031, 3, 5), 2, 5)
            .Should().Equal(new DateOnly(2031, 1, 1), new DateOnly(2031, 2, 1));
        PlanoColetaIndicadores.MesesFechados(new DateOnly(2031, 1, 15), 2)
            .Should().Equal(new DateOnly(2030, 11, 1), new DateOnly(2030, 12, 1));
    }

    // ========================================================================= trabalhos

    private sealed class SessaoRoteirizada(params string[] respostas) : ISisregWebSessao
    {
        private int _i;
        public List<(string Caminho, IReadOnlyDictionary<string, string>? Campos)> Pedidos { get; } = [];
        public Exception? Lancar { get; init; }

        private Task<string> Proxima(string caminho, IReadOnlyDictionary<string, string>? campos)
        {
            Pedidos.Add((caminho, campos));
            if (Lancar is not null) throw Lancar;
            return Task.FromResult(respostas[Math.Min(_i++, respostas.Length - 1)]);
        }

        public Task<string> PostFormAsync(string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken) =>
            Proxima(caminho, campos);

        public Task<string> GetAsync(string caminho, IReadOnlyDictionary<string, string>? query, CancellationToken cancellationToken,
            Func<string, bool>? pareceSessaoCaida = null) => Proxima(caminho, query);

        public void UsarCredencialDoOperador(string usuario, string senha) { }
    }

    private sealed class ArmazemFalso : IArmazemIndicadoresSisreg
    {
        public int FaltasGravadas { get; set; }
        public int? FaltasSubstituidas { get; private set; }
        public int PpiGravadas { get; set; }
        public int? PpiSubstituidas { get; private set; }
        public List<MarcacaoCanceladaLidaSisreg> Canceladas { get; } = [];
        public List<(string Cnes, DesfechoLidoSisreg, SituacaoDesfechoSisreg)> Desfechos { get; } = [];

        public Task<int> ContarFaltasAsync(DateOnly inicio, DateOnly fim, CancellationToken ct) => Task.FromResult(FaltasGravadas);

        public Task SubstituirFaltasAsync(DateOnly inicio, DateOnly fim, IReadOnlyList<FaltaLidaSisreg> faltas, CancellationToken ct)
        {
            FaltasSubstituidas = faltas.Count;
            return Task.CompletedTask;
        }

        public Task<int> ContarPpiAsync(DateOnly competencia, CancellationToken ct) => Task.FromResult(PpiGravadas);

        public Task SubstituirPpiAsync(DateOnly competencia, IReadOnlyList<CotaPpiLidaSisreg> cotas, CancellationToken ct)
        {
            PpiSubstituidas = cotas.Count;
            return Task.CompletedTask;
        }

        public Task<int> GravarCanceladasAsync(IReadOnlyList<MarcacaoCanceladaLidaSisreg> canceladas, CancellationToken ct)
        {
            Canceladas.AddRange(canceladas);
            return Task.FromResult(canceladas.Count);
        }

        public Task<int> GravarDesfechosAsync(string cnesSolicitante,
            IReadOnlyList<(DesfechoLidoSisreg Linha, SituacaoDesfechoSisreg Situacao)> desfechos, CancellationToken ct)
        {
            Desfechos.AddRange(desfechos.Select(d => (cnesSolicitante, d.Linha, d.Situacao)));
            return Task.FromResult(desfechos.Count);
        }

        public Task RegistrarJanelaConcluidaAsync(ColetorIndicadorSisreg coletor, DateOnly inicio, DateOnly fim, string escopo,
            int linhas, CancellationToken ct) => Task.CompletedTask;
    }

    private static ItemColeta Item(ColetorIndicadorSisreg c, string escopo = "") =>
        new(Guid.NewGuid(), c, new DateOnly(2031, 1, 9), new DateOnly(2031, 1, 16), escopo);

    [Fact]
    public async Task Faltas_conferem_a_lista_inteira_com_a_paginacao_antes_de_substituir()
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: 2, linhas: 10), Faltas(paginas: null, linhas: 15));
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7);

        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Continuar);
        var r = await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);

        r.Should().Be(ResultadoPasso.Concluida(15));
        armazem.FaltasSubstituidas.Should().Be(15);
        sessao.Pedidos[0].Campos!.ContainsKey("imprimir_lista").Should().BeFalse();
        sessao.Pedidos[1].Campos!["imprimir_lista"].Should().Be("1");
        sessao.Pedidos[1].Campos!["data1"].Should().Be("09/01/2031");
    }

    [Fact]
    public async Task Faltas_que_nao_cabem_na_paginacao_nao_gravam_nada()
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: 2, linhas: 10), Faltas(paginas: null, linhas: 25));
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7);

        await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);
        var r = await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);

        r.Desfecho.Should().Be(DesfechoPasso.Falha);
        armazem.FaltasSubstituidas.Should().BeNull();
    }

    /// <summary>
    /// Dia com até 10 faltas: o SISREG não escreve "Mostrando Página de N" quando há uma página só.
    /// 12/09, 27/09 e 04/10/2026 (9, 8 e 3 faltas) ficavam em falha para sempre exigindo o rodapé.
    /// </summary>
    [Fact]
    public async Task Faltas_de_uma_pagina_so_fecham_sem_o_rodape()
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: null, linhas: 9), Faltas(paginas: null, linhas: 9));
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.FaltasRecentes, "dia"), 0.7, recentes: true);

        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Continuar);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Should().Be(ResultadoPasso.Concluida(9));
        armazem.FaltasSubstituidas.Should().Be(9);
    }

    [Fact]
    public async Task Pagina_sem_rodape_com_mais_de_dez_linhas_nao_vale_como_uma_pagina()
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: null, linhas: 11));
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.FaltasRecentes, "dia"), 0.7, recentes: true);

        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Falha);
        armazem.FaltasSubstituidas.Should().BeNull();
    }

    /// <summary>
    /// 14/09/2026: a lista inteira trouxe 310 e a paginação da rede dizia 32 páginas (311–320). Lido
    /// por unidade executante, as 13 listas bateram com a própria paginação e somaram 310 — a sobra
    /// é da contagem do SISREG. Uma ou duas a menos gravam, com aviso; mais que isso continua falha.
    /// </summary>
    [Theory]
    [InlineData(310, true)]
    [InlineData(309, true)]
    [InlineData(308, false)]
    public async Task Lista_ate_duas_abaixo_da_paginacao_grava_com_aviso(int linhas, bool grava)
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: 32, linhas: 10), Faltas(paginas: null, linhas: linhas));
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.FaltasRecentes, "dia"), 0.7, recentes: true);

        await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);
        var r = await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);

        if (grava)
        {
            r.Desfecho.Should().Be(DesfechoPasso.Concluida);
            r.Linhas.Should().Be(linhas);
            r.Mensagem.Should().Contain("a partir de 311");
            armazem.FaltasSubstituidas.Should().Be(linhas);
        }
        else
        {
            r.Desfecho.Should().Be(DesfechoPasso.Falha);
            armazem.FaltasSubstituidas.Should().BeNull();
        }
    }

    [Fact]
    public async Task Faltas_com_trava_de_encolhimento_nao_apagam_a_janela_gravada()
    {
        var sessao = new SessaoRoteirizada(Faltas(paginas: 2, linhas: 10), Faltas(paginas: null, linhas: 15));
        var armazem = new ArmazemFalso { FaltasGravadas = 100 };
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7);

        await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);
        var r = await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);

        r.Desfecho.Should().Be(DesfechoPasso.Falha);
        r.Mensagem.Should().Contain("encolheu");
        armazem.FaltasSubstituidas.Should().BeNull();
    }

    /// <summary>
    /// A lista das semanas recentes ainda muda (a unidade aponta e corrige com atraso). Se a trava de
    /// encolhimento valesse, a janela ficaria presa no retrato antigo e quem a unidade já trocou de
    /// falta para chegada confirmada continuaria aparecendo como falta.
    /// </summary>
    [Theory]
    [InlineData(true, false)]   // releitura horária da semana recente
    [InlineData(false, true)]   // leitura oficial por cima do que a recente gravou
    public async Task Faltas_recentes_encolhem_sem_a_trava_mas_com_a_mesma_prova(bool recentes, bool substituiProvisoria)
    {
        var armazem = new ArmazemFalso { FaltasGravadas = 100 };
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.FaltasRecentes), 0.7, recentes, substituiProvisoria);
        var sessao = new SessaoRoteirizada(Faltas(paginas: 2, linhas: 10), Faltas(paginas: null, linhas: 15));

        await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Should().Be(ResultadoPasso.Concluida(15));
        armazem.FaltasSubstituidas.Should().Be(15);

        // A prova de leitura completa continua valendo: lista que não cabe na paginação não grava.
        var semProva = new ArmazemFalso { FaltasGravadas = 100 };
        var t2 = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.FaltasRecentes), 0.7, recentes, substituiProvisoria);
        var cortada = new SessaoRoteirizada(Faltas(paginas: 2, linhas: 10), Faltas(paginas: null, linhas: 25));
        await TrabalhoColeta.ExecutarAsync(t2, cortada, semProva, default);
        (await TrabalhoColeta.ExecutarAsync(t2, cortada, semProva, default)).Desfecho.Should().Be(DesfechoPasso.Falha);
        semProva.FaltasSubstituidas.Should().BeNull();
    }

    [Fact]
    public async Task Semana_sem_faltas_so_fecha_com_nenhum_registro_dito_pela_tela()
    {
        var vazia = Faltas(paginas: null, linhas: 0, nenhum: true);
        var armazem = new ArmazemFalso();
        var t = new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7);
        var sessao = new SessaoRoteirizada(vazia, vazia);

        await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Should().Be(ResultadoPasso.Concluida(0));
    }

    [Fact]
    public async Task Pagina_de_gateway_e_tempo_esgotado_e_login_perdido_e_falha_sem_gravar()
    {
        var armazem = new ArmazemFalso();
        const string gateway = "<html><head><title>504 Gateway Time-out</title></head></html>";
        (await TrabalhoColeta.ExecutarAsync(new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7),
                new SessaoRoteirizada(gateway), armazem, default))
            .Desfecho.Should().Be(DesfechoPasso.TempoEsgotado);

        // Tela de login (a sessão caiu e o relogin não resolveu): não é a tela esperada.
        const string login = "<html><body><form><input name=\"senha_256\"></form></body></html>";
        (await TrabalhoColeta.ExecutarAsync(new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7),
                new SessaoRoteirizada(login), armazem, default))
            .Desfecho.Should().Be(DesfechoPasso.Falha);

        (await TrabalhoColeta.ExecutarAsync(new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7),
                new SessaoRoteirizada("x") { Lancar = new HttpRequestException("connection reset") }, armazem, default))
            .Desfecho.Should().Be(DesfechoPasso.TempoEsgotado);
        armazem.FaltasSubstituidas.Should().BeNull();
    }

    [Fact]
    public async Task Captcha_para_o_passo_na_hora()
    {
        var sessao = new SessaoRoteirizada("x")
        {
            Lancar = new ValidacaoException(SisregWebSessao.CodigoCaptcha, "CAPTCHA"),
        };

        var r = await TrabalhoColeta.ExecutarAsync(new TrabalhoPpi(Item(ColetorIndicadorSisreg.Ppi), "330270", 0.7),
            sessao, new ArmazemFalso(), default);

        r.Desfecho.Should().Be(DesfechoPasso.Captcha);
    }

    /// <summary>
    /// Login recusado não é culpa da consulta. Até 10/10/2026 a exceção escapava do passo: o item voltava
    /// a pendente como órfão a cada tick e, em meia hora de login recusado (09/10), 10 leituras de faltas
    /// gastaram as 6 tentativas e ficaram paradas sem erro na tela.
    /// </summary>
    [Fact]
    public async Task Login_recusado_e_desfecho_proprio_e_nao_grava_nada()
    {
        var sessao = new SessaoRoteirizada("x")
        {
            Lancar = new ValidacaoException(SisregWebSessao.CodigoLoginFalhou, "Não foi possível autenticar no SISREG."),
        };
        var armazem = new ArmazemFalso();

        var r = await TrabalhoColeta.ExecutarAsync(new TrabalhoFaltas(Item(ColetorIndicadorSisreg.Faltas), 0.7),
            sessao, armazem, default);

        r.Desfecho.Should().Be(DesfechoPasso.LoginRecusado);
        armazem.FaltasSubstituidas.Should().BeNull();
    }

    [Fact]
    public async Task Canceladas_do_mes_gravam_pagina_a_pagina_e_so_fecham_batendo_com_o_declarado()
    {
        var armazem = new ArmazemFalso();
        var t = new TrabalhoCanceladasMes(Item(ColetorIndicadorSisreg.Canceladas));
        var sessao = new SessaoRoteirizada(Canceladas(3, 2, "111111111", "222222222"), Canceladas(3, 2, "333333333"));

        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Continuar);
        armazem.Canceladas.Should().HaveCount(2, "a página é gravada na hora (upsert, não apaga nada)");
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Should().Be(ResultadoPasso.Concluida(3));
        sessao.Pedidos.Select(p => p.Campos!["pagina"]).Should().Equal("0", "1");

        var incompleto = new TrabalhoCanceladasMes(Item(ColetorIndicadorSisreg.Canceladas));
        var faltando = new SessaoRoteirizada(Canceladas(3, 2, "111111111"), Canceladas(3, 2, "333333333"));
        await TrabalhoColeta.ExecutarAsync(incompleto, faltando, new ArmazemFalso(), default);
        (await TrabalhoColeta.ExecutarAsync(incompleto, faltando, new ArmazemFalso(), default)).Desfecho
            .Should().Be(DesfechoPasso.Falha);
    }

    /// <summary>
    /// A lista repete a mesma linha em páginas vizinhas: set/2026 declarou 1486 e a soma crua deu 1487 —
    /// o mês nunca fechava, com as 1486 gravadas. O que conta é o cancelamento distinto (código + quando).
    /// </summary>
    [Fact]
    public async Task Canceladas_com_linha_repetida_entre_paginas_fecham_pelo_distinto()
    {
        var t = new TrabalhoCanceladasMes(Item(ColetorIndicadorSisreg.Canceladas));
        var sessao = new SessaoRoteirizada(
            Canceladas(3, 2, "111111111", "222222222"), Canceladas(3, 2, "222222222", "333333333"));

        await TrabalhoColeta.ExecutarAsync(t, sessao, new ArmazemFalso(), default);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, new ArmazemFalso(), default))
            .Should().Be(ResultadoPasso.Concluida(3));
    }

    [Fact]
    public async Task Desfechos_leem_as_tres_situacoes_da_unidade_e_so_gravam_no_fim()
    {
        var armazem = new ArmazemFalso();
        var t = new TrabalhoDesfechos(Item(ColetorIndicadorSisreg.Desfechos, "2266741"));
        var sessao = new SessaoRoteirizada(Gerenciador(2, 2), Gerenciador(null, 0) + "Nenhum registro", Gerenciador(1, 1));

        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Continuar);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Desfecho.Should().Be(DesfechoPasso.Continuar);
        armazem.Desfechos.Should().BeEmpty();
        (await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default)).Should().Be(ResultadoPasso.Concluida(3));

        armazem.Desfechos.Should().OnlyContain(d => d.Cnes == "2266741");
        armazem.Desfechos.Select(d => d.Item3).Distinct().Should().BeEquivalentTo(
            [SituacaoDesfechoSisreg.Devolvida, SituacaoDesfechoSisreg.CanceladaAntesDeAgendar]);
        sessao.Pedidos.Select(p => p.Campos!["cmb_situacao"]).Should().Equal("4", "6", "3");
        sessao.Pedidos.Should().OnlyContain(p => p.Campos!["cnes_solicitante"] == "2266741");
    }

    [Fact]
    public async Task Desfecho_com_total_que_nao_bate_nao_grava()
    {
        var armazem = new ArmazemFalso();
        var t = new TrabalhoDesfechos(Item(ColetorIndicadorSisreg.Desfechos, "2266741"));

        var r = await TrabalhoColeta.ExecutarAsync(t, new SessaoRoteirizada(Gerenciador(5, 3)), armazem, default);

        r.Desfecho.Should().Be(DesfechoPasso.Falha);
        armazem.Desfechos.Should().BeEmpty();
    }

    [Fact]
    public async Task Ppi_sem_codigo_do_municipio_falha_sem_ir_ao_SISREG()
    {
        var sessao = new SessaoRoteirizada(Ppi(("1", 1, 1, "0")));

        var r = await TrabalhoColeta.ExecutarAsync(new TrabalhoPpi(Item(ColetorIndicadorSisreg.Ppi), null, 0.7),
            sessao, new ArmazemFalso(), default);

        r.Desfecho.Should().Be(DesfechoPasso.Falha);
        sessao.Pedidos.Should().BeEmpty();
    }

    [Fact]
    public async Task Ppi_substitui_a_competencia_com_o_municipio_da_instituicao()
    {
        var sessao = new SessaoRoteirizada(Ppi(("1801026", 1000, 2, "998"), ("2018220", 10, 0, "10")));
        var armazem = new ArmazemFalso();

        var r = await TrabalhoColeta.ExecutarAsync(new TrabalhoPpi(Item(ColetorIndicadorSisreg.Ppi), "123456", 0.7),
            sessao, armazem, default);

        r.Should().Be(ResultadoPasso.Concluida(2));
        armazem.PpiSubstituidas.Should().Be(2);
        sessao.Pedidos[0].Campos!["exec"].Should().Be("123456");
        sessao.Pedidos[0].Campos!["mes"].Should().Be("1");
    }

    // ================================================================ amostra de motivos

    [Fact]
    public void Paginas_da_amostra_se_espalham_do_inicio_ao_fim()
    {
        PlanoColetaIndicadores.PaginasDaAmostra(119, 6).Should().Equal(0, 24, 47, 71, 94, 118);
        PlanoColetaIndicadores.PaginasDaAmostra(3, 6).Should().Equal(0, 1, 2);
        PlanoColetaIndicadores.PaginasDaAmostra(1, 6).Should().Equal(0);
    }

    [Fact]
    public async Task Amostra_le_a_primeira_pagina_e_as_espalhadas_e_guarda_o_codigo()
    {
        var armazem = new ArmazemFalso();
        var t = new TrabalhoCanceladasAmostra(Item(ColetorIndicadorSisreg.Canceladas, "amostra"), 6);
        var sessao = new SessaoRoteirizada(
            Canceladas(2368, 119, "100000001", "100000002"),
            Canceladas(2368, 119, "100000003"), Canceladas(2368, 119, "100000004"), Canceladas(2368, 119, "100000005"),
            Canceladas(2368, 119, "100000006"), Canceladas(2368, 119, "100000007"));

        ResultadoPasso r;
        var passos = 0;
        do { r = await TrabalhoColeta.ExecutarAsync(t, sessao, armazem, default); passos++; }
        while (r.Desfecho == DesfechoPasso.Continuar && passos < 20);

        r.Should().Be(ResultadoPasso.Concluida(7), "a janela da amostra registra o que LEU, não o total do mês");
        sessao.Pedidos.Select(p => p.Campos!["pagina"]).Should().Equal("0", "24", "47", "71", "94", "118");
        armazem.Canceladas.Select(c => c.Codigo).Should().HaveCount(7).And.OnlyContain(c => c.StartsWith("10000000"));
    }

    [Fact]
    public async Task Pagina_do_meio_vazia_derruba_a_amostra()
    {
        var t = new TrabalhoCanceladasAmostra(Item(ColetorIndicadorSisreg.Canceladas, "amostra"), 6);
        var sessao = new SessaoRoteirizada(Canceladas(200, 10, "100000001"), Canceladas(200, 10));

        await TrabalhoColeta.ExecutarAsync(t, sessao, new ArmazemFalso(), default);
        (await TrabalhoColeta.ExecutarAsync(t, sessao, new ArmazemFalso(), default)).Desfecho.Should().Be(DesfechoPasso.Falha);
    }
}
