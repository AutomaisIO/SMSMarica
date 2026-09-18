using System.Text.Json;
using SMSMais.Core.EstrategiasFila;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// A trava é contrato, não instrução: o que o modelo mandar contra um parâmetro travado tem de ser
/// rejeitado aqui, antes de virar projeção. Estes testes prendem isso.
/// </summary>
public class ParametrosEstrategiaAplicadorTests
{
    private static ParametrosEstrategia Vigentes() => new(
        Objetivo: ObjetivoEstrategia.ZerarEmSemanas,
        PrazoAlvoSemanas: 12,
        Unidades: new ParametroNumero(3, Travado: true),
        Profissionais: new ParametroNumero(10, Travado: false, Min: 0, Max: 20),
        DiasPorSemana: new ParametroNumero(5, false, 0, 7),
        HorasPorDia: new ParametroNumero(4, false, 0, 12),
        AtendimentosPorHora: new ParametroNumero(2, false, 0, null),
        Aproveitamento: new ParametroNumero(0.8, true, 0, 1),
        EntradaSemanal: new ParametroNumero(100, true, 0, null),
        Mutiroes: [new MutiraoDto(2, 50)],
        MutiroesTravados: false,
        HorizonteSemanas: 104);

    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Parametro_livre_muda_e_travado_igual_passa()
    {
        var (r, violacoes) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "profissionais": 14, "unidades": 3, "aproveitamento": 0.8 }"""));

        violacoes.Should().BeEmpty();
        r.Profissionais.Valor.Should().Be(14);
        r.Profissionais.Travado.Should().BeFalse();
        r.Unidades.Valor.Should().Be(3);
        // O resto fica como estava.
        r.DiasPorSemana.Valor.Should().Be(5);
        r.EntradaSemanal.Valor.Should().Be(100);
    }

    [Fact]
    public void Travado_diferente_e_rejeitado_e_o_valor_nao_muda()
    {
        var (r, violacoes) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "unidades": 5, "entradaSemanal": 50, "profissionais": 12 }"""));

        violacoes.Should().HaveCount(2);
        violacoes.Should().Contain(v => v.StartsWith("unidades está travado"));
        violacoes.Should().Contain(v => v.StartsWith("entradaSemanal está travado"));
        r.Unidades.Valor.Should().Be(3);
        r.EntradaSemanal.Valor.Should().Be(100);
        // O livre válido foi aplicado mesmo assim — quem decide o que fazer com a violação é o chamador.
        r.Profissionais.Valor.Should().Be(12);
    }

    [Fact]
    public void Livre_fora_do_intervalo_e_rejeitado()
    {
        var (r, violacoes) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "profissionais": 25, "diasPorSemana": -1 }"""));

        violacoes.Should().HaveCount(2);
        r.Profissionais.Valor.Should().Be(10);
        r.DiasPorSemana.Valor.Should().Be(5);
    }

    [Fact]
    public void Mutiroes_substituem_a_lista_quando_livres_e_sao_rejeitados_quando_travados()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(
            Vigentes(), J("""{ "mutiroes": [ { "semana": 1, "vagas": 200, "descricao": "sábado" }, { "semana": 0, "vagas": 9 } ] }"""));
        v.Should().BeEmpty();
        r.Mutiroes.Should().ContainSingle().Which.Vagas.Should().Be(200);

        var travados = Vigentes() with { MutiroesTravados = true };
        var (r2, v2) = ParametrosEstrategiaAplicador.Aplicar(
            travados, J("""{ "mutiroes": [ { "semana": 3, "vagas": 10 } ] }"""));
        v2.Should().ContainSingle().Which.Should().StartWith("mutiroes está travado");
        r2.Mutiroes.Should().BeEquivalentTo(travados.Mutiroes);

        // Mandar a mesma lista com mutirões travados não é violação.
        var (_, v3) = ParametrosEstrategiaAplicador.Aplicar(
            travados, J("""{ "mutiroes": [ { "semana": 2, "vagas": 50 } ] }"""));
        v3.Should().BeEmpty();
    }

    [Fact]
    public void Entrada_vazia_ou_invalida_nao_muda_nada()
    {
        var (r, v) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), J("""{}"""));
        v.Should().BeEmpty();
        r.Should().BeEquivalentTo(Vigentes());

        var (r2, v2) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), J("""{ "profissionais": "dez" }"""));
        v2.Should().BeEmpty();
        r2.Profissionais.Valor.Should().Be(10);

        var (r3, v3) = ParametrosEstrategiaAplicador.Aplicar(Vigentes(), default);
        v3.Should().BeEmpty();
        r3.Should().BeEquivalentTo(Vigentes());
    }
}
