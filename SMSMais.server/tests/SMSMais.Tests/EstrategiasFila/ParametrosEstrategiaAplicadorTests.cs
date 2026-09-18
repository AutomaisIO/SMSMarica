using System.Text.Json;
using SMSMais.Core.EstrategiasFila;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// A trava é contrato, não instrução: o que o modelo mandar contra uma linha travada, ou contra a
/// realidade (médico que já tem escala em outro lugar naquele dia), tem de ser rejeitado aqui,
/// antes de virar projeção. Estes testes prendem isso.
/// </summary>
public class ParametrosEstrategiaAplicadorTests
{
    // Fixos: `Vigentes()` é chamado duas vezes no mesmo teste e comparado por equivalência.
    private static readonly Guid UnidadeCdt = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnidadeCmi = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ParametrosEstrategia Vigentes() => new(
        Objetivo: ObjetivoEstrategia.ZerarEmSemanas,
        PrazoAlvoSemanas: 12,
        Quadro:
        [
            // Real, livre, atende seg e qua; já tem mamografia na ter e qui.
            new LinhaQuadro("jose", "JOSE", false, UnidadeCdt, "CDT", [1, 3], 10, false, [1, 3],
                new Dictionary<int, string> { [2] = "CDT · MAMOGRAFIA", [4] = "CDT · MAMOGRAFIA" }),
            // Real, TRAVADO.
            new LinhaQuadro("ana", "ANA", false, UnidadeCmi, "CMI", [5], 12, true, [5], new Dictionary<int, string>()),
            // Simulação, livre.
            new LinhaQuadro("sim-1", "Médico simulação 1", true, null, "Unidade simulação", [1], 10, false, [], new Dictionary<int, string>()),
        ],
        UnidadesSimuladas: ["Unidade simulação"],
        PermitirNovosProfissionais: true,
        MaxNovosProfissionais: 2,
        Aproveitamento: new ParametroNumero(0.8, true, 0, 1),
        EntradaSemanal: new ParametroNumero(100, true, 0, null),
        Mutiroes: [new MutiraoDto(2, 50)],
        MutiroesTravados: false,
        HorizonteSemanas: 104);

    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Acende_dia_livre_e_muda_atendimentos_de_linha_livre()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "quadro": [ { "id": "jose", "dias": [1, 3, 5, 6], "atendimentosPorTurno": 12 } ] }"""));

        v.Should().BeEmpty();
        var jose = r.Quadro.Single(l => l.Id == "jose");
        jose.Dias.Should().Equal(1, 3, 5, 6);
        jose.AtendimentosPorTurno.Should().Be(12);
        jose.DiasReais.Should().Equal(1, 3);
        // O resto fica como estava.
        r.Quadro.Single(l => l.Id == "ana").Dias.Should().Equal(5);
        r.Quadro.Should().HaveCount(3);
    }

    [Fact]
    public void Medico_real_nao_ganha_dia_em_que_ja_tem_outra_escala()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "quadro": [ { "id": "jose", "dias": [1, 2, 3] } ] }"""));

        v.Should().ContainSingle().Which.Should().Contain("JOSE já tem escala em ter").And.Contain("MAMOGRAFIA");
        r.Quadro.Single(l => l.Id == "jose").Dias.Should().Equal(1, 3);
    }

    [Fact]
    public void Linha_travada_nao_muda_e_id_desconhecido_e_rejeitado()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "quadro": [ { "id": "ana", "dias": [5, 6] }, { "id": "zzz", "dias": [1] } ] }"""));

        v.Should().HaveCount(2);
        v.Should().Contain(x => x.StartsWith("ANA está TRAVADO"));
        v.Should().Contain(x => x.Contains("'zzz' não existe"));
        r.Quadro.Single(l => l.Id == "ana").Dias.Should().Equal(5);

        // Mandar a linha travada igual não é violação.
        var (_, v2) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "quadro": [ { "id": "ana", "dias": [5], "atendimentosPorTurno": 12 } ] }"""));
        v2.Should().BeEmpty();
    }

    [Fact]
    public void Novos_substituem_os_simulados_livres_e_respeitam_o_maximo()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "novos": [ { "nome": "Novo A", "unidade": "CDT", "dias": [2, 4] }, { "dias": [1], "unidade": "Unidade nova" } ] }"""));

        v.Should().BeEmpty();
        r.Quadro.Should().NotContain(l => l.Id == "sim-1");
        var novos = r.Quadro.Where(l => l.Simulado).ToList();
        novos.Should().HaveCount(2);
        novos[0].Nome.Should().Be("Novo A");
        novos[0].Unidade.Should().Be("CDT");
        // Sem atendimentos informados, herda a média do quadro (10×2 + 12×1 + 10×1 = 42 vagas / 4 turnos).
        novos[0].AtendimentosPorTurno.Should().Be(10.5);
        novos[1].Nome.Should().Be("Médico simulação 2");
        r.UnidadesSimuladas.Should().Contain("Unidade nova");
        // A "sim-1" removida também sai do quadro: os reais ficam.
        r.Quadro.Count(l => !l.Simulado).Should().Be(2);

        var (r3, v3) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "novos": [ { "dias": [1] }, { "dias": [1] }, { "dias": [1] } ] }"""));
        v3.Should().ContainSingle().Which.Should().StartWith("No máximo 2");
        r3.Quadro.Count(l => l.Simulado).Should().Be(1);

        var (r4, v4) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes() with { PermitirNovosProfissionais = false }, J("""{ "novos": [ { "dias": [1] } ] }"""));
        v4.Should().ContainSingle().Which.Should().Contain("Não é permitido");
        r4.Quadro.Count(l => l.Simulado).Should().Be(1);
    }

    [Fact]
    public void Numericos_travados_e_mutiroes_seguem_a_regra_antiga()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "entradaSemanal": 50, "mutiroes": [ { "semana": 1, "vagas": 200 } ] }"""));
        v.Should().ContainSingle().Which.Should().StartWith("entradaSemanal está travado");
        r.EntradaSemanal.Valor.Should().Be(100);
        r.Mutiroes.Should().ContainSingle().Which.Vagas.Should().Be(200);

        var travados = Vigentes() with { MutiroesTravados = true };
        var (_, v2) = ParametrosEstrategiaAplicador.Aplicar(travados, J("""{ "mutiroes": [ { "semana": 3, "vagas": 10 } ] }"""));
        v2.Should().ContainSingle().Which.Should().StartWith("mutiroes está travado");
    }

    [Fact]
    public void Entrada_vazia_ou_invalida_nao_muda_nada()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), J("""{}"""));
        v.Should().BeEmpty();
        r.Should().BeEquivalentTo(Vigentes());

        var (r2, v2) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), J("""{ "quadro": [ { "id": "jose", "dias": [1, 3, 7] } ] }"""));
        v2.Should().ContainSingle().Which.Should().Contain("dia inválido");
        r2.Quadro.Single(l => l.Id == "jose").Dias.Should().Equal(1, 3);

        var (r3, v3) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), default);
        v3.Should().BeEmpty();
        r3.Should().BeEquivalentTo(Vigentes());
    }
}
