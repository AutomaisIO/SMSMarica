using SMSMais.Core.EstrategiasFila;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// O simulador é a peça que o gestor leva ao secretário: "zera em N semanas". Estes testes prendem
/// a conta no papel — sem banco, sem modelo — para que ninguém possa mudar a fórmula sem um teste
/// vermelho dizendo o quê.
/// </summary>
public class SimuladorFilaTests
{
    private static ParametroNumero N(double v, bool travado = false) => new(v, travado);

    /// <summary>Uma linha do quadro: 5 dias (seg–sex) × 8 por turno = 40 vagas/semana.</summary>
    public static LinhaQuadro Linha(string id, double atendPorTurno = 8, params int[] dias) => new(
        id, $"Médico {id}", Simulado: false, null, "Unidade A",
        Dias: dias.Length == 0 ? [1, 2, 3, 4, 5] : dias,
        AtendimentosPorTurno: atendPorTurno, Travado: false, DiasReais: [1, 2, 3, 4, 5],
        OutrasEscalas: new Dictionary<int, string>());

    /// <summary>10 profissionais × 5 turnos/semana × 8 por turno × 100% = 400/semana.</summary>
    public static ParametrosEstrategia Base(
        double entrada = 100, double aproveitamento = 1, int profissionais = 10,
        int? prazo = null, IReadOnlyList<MutiraoDto>? mutiroes = null, int horizonte = 104) =>
        new(
            Objetivo: ObjetivoEstrategia.ZerarEmSemanas,
            PrazoAlvoSemanas: prazo,
            Quadro: [.. Enumerable.Range(1, profissionais).Select(i => Linha($"p{i}"))],
            UnidadesSimuladas: [],
            PermitirNovosProfissionais: true,
            MaxNovosProfissionais: 5,
            Aproveitamento: N(aproveitamento),
            EntradaSemanal: N(entrada),
            Mutiroes: mutiroes ?? [],
            MutiroesTravados: false,
            HorizonteSemanas: horizonte);

    [Fact]
    public void Capacidade_e_a_soma_das_linhas_vezes_o_aproveitamento()
    {
        var p = Base(aproveitamento: 0.75);
        p.TurnosSemanais().Should().Be(50);
        p.VagasSemanais().Should().Be(400);
        p.CapacidadeSemanal().Should().Be(300);

        // Uma linha com dias repetidos ou inválidos não conta duas vezes nem explode.
        var torta = Linha("x", 10, 1, 1, 9, -1, 3);
        torta.Turnos.Should().Be(2);
        torta.VagasSemana.Should().Be(20);
    }

    [Fact]
    public void Zera_quando_a_capacidade_supera_a_entrada()
    {
        // Fila 1.000, entram 100, atende 400: sobra 300 líquidas por semana → 4 semanas
        // (1000 → 700 → 400 → 100 → 0).
        var proj = SimuladorFila.Projetar(Base(entrada: 100), filaInicial: 1000);

        proj.Zera.Should().BeTrue();
        proj.SemanaZera.Should().Be(4);
        proj.FilaFinal.Should().Be(0);
        proj.CrescimentoSemanal.Should().Be(0);
        proj.CapacidadeEquilibrio.Should().Be(100);
        proj.AtendidosAteZerar.Should().Be(1000 + 4 * 100);
        proj.Serie.Should().HaveCount(105);
        proj.Serie[0].Fila.Should().Be(1000);
        proj.Serie[4].Fila.Should().Be(0);
    }

    [Fact]
    public void Nao_zera_quando_a_entrada_e_maior_que_a_capacidade_e_diz_quanto_cresce()
    {
        var proj = SimuladorFila.Projetar(Base(entrada: 450), filaInicial: 1000);

        proj.Zera.Should().BeFalse();
        proj.SemanaZera.Should().BeNull();
        proj.CrescimentoSemanal.Should().Be(50);
        proj.FilaFinal.Should().Be(1000 + 50 * 104);
        proj.PicoFila.Should().Be(proj.FilaFinal);
        proj.CapacidadeEquilibrio.Should().Be(450);
    }

    [Fact]
    public void Acender_um_dia_numa_linha_aumenta_a_capacidade_daquela_linha()
    {
        var p = Base(entrada: 100);
        var comSabado = p with { Quadro = [.. p.Quadro.Select((l, i) => i == 0 ? l with { Dias = [1, 2, 3, 4, 5, 6] } : l)] };

        comSabado.CapacidadeSemanal().Should().Be(408);
        SimuladorFila.Projetar(comSabado, 1000).SemanaZera.Should().BeLessThanOrEqualTo(SimuladorFila.Projetar(p, 1000).SemanaZera!.Value);
    }

    [Fact]
    public void Aproveitamento_baixo_reduz_a_capacidade_efetiva()
    {
        var proj = SimuladorFila.Projetar(Base(entrada: 100, aproveitamento: 0.5), filaInicial: 1000);

        proj.CapacidadeSemanal.Should().Be(200);
        proj.VagasSemanais.Should().Be(400);
        proj.SemanaZera.Should().Be(10);
    }

    [Fact]
    public void Mutirao_abate_a_fila_na_semana_em_que_acontece()
    {
        var sem = SimuladorFila.Projetar(Base(entrada: 100), 1000);
        var com = SimuladorFila.Projetar(
            Base(entrada: 100, mutiroes: [new MutiraoDto(1, 500, "mutirão de sábado")]), 1000);

        com.Serie[1].Capacidade.Should().Be(900);
        com.Serie[1].Fila.Should().Be(1000 + 100 - 900);
        com.SemanaZera.Should().BeLessThan(sem.SemanaZera!.Value);
        SimuladorFila.Projetar(Base(mutiroes: [new MutiraoDto(0, 10), new MutiraoDto(5, 0)]), 1000)
            .SemanaZera.Should().Be(sem.SemanaZera);
    }

    [Fact]
    public void Capacidade_necessaria_para_zerar_no_prazo_e_a_fila_mais_a_entrada_do_periodo()
    {
        var proj = SimuladorFila.Projetar(Base(entrada: 100, prazo: 5), 1000);
        proj.CapacidadeParaZerarNoPrazo.Should().Be(300);

        var comMutirao = SimuladorFila.Projetar(
            Base(entrada: 100, prazo: 5, mutiroes: [new MutiraoDto(2, 500)]), 1000);
        comMutirao.CapacidadeParaZerarNoPrazo.Should().Be(200);

        SimuladorFila.Projetar(Base(entrada: 100), 1000).CapacidadeParaZerarNoPrazo.Should().BeNull();
    }

    [Fact]
    public void Turnos_a_mais_para_uma_capacidade_alvo()
    {
        // Capacidade hoje 400; para 480 faltam 80 → 10 turnos de 8.
        SimuladorFila.TurnosAMaisPara(Base(), 480, 8).Should().Be(10);
        SimuladorFila.TurnosAMaisPara(Base(), 300, 8).Should().Be(0);
        SimuladorFila.TurnosAMaisPara(Base(), 480, 0).Should().BeNull();
    }

    [Fact]
    public void Quadro_vazio_nao_explode_e_nao_zera_se_continua_entrando()
    {
        var semOferta = SimuladorFila.Projetar(Base(entrada: 10, profissionais: 0), 0);
        semOferta.CapacidadeSemanal.Should().Be(0);
        semOferta.Zera.Should().BeFalse();
        semOferta.FilaFinal.Should().Be(10 * 104);
        Base(profissionais: 0).AtendimentosPorTurnoPadrao().Should().Be(ParametrosEstrategia.AtendimentosPorTurnoPadraoSemOferta);
        Base().AtendimentosPorTurnoPadrao().Should().Be(8);

        var nada = SimuladorFila.Projetar(Base(entrada: 0, profissionais: 0), 0);
        nada.Zera.Should().BeTrue();
        nada.SemanaZera.Should().Be(1);
    }

    [Fact]
    public void Horizonte_e_limitado_ao_maximo_e_ao_minimo()
    {
        SimuladorFila.Projetar(Base(horizonte: 9999), 10).HorizonteSemanas
            .Should().Be(ParametrosEstrategia.HorizonteMaximo);
        SimuladorFila.Projetar(Base(horizonte: 0), 10).HorizonteSemanas
            .Should().Be(ParametrosEstrategia.HorizontePadrao);
    }
}
