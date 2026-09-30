using SMSMais.Core.Tratamentos;
using static SMSMais.Tests.Tratamentos.TransporteFabrica;

namespace SMSMais.Tests.Tratamentos;

/// <summary>
/// A agenda do atendimento é "dias da semana + N sessões" ou "contínuo". Contínuo nunca é infinito:
/// vai até o fim do mês seguinte e a renovação estende de mês em mês.
/// </summary>
public class AgendaDeSessoesTests
{
    [Fact]
    public void N_sessoes_caem_so_nos_dias_marcados_a_partir_do_inicio()
    {
        // 01/10/2026 é quinta-feira: a primeira sessão Seg/Qua/Sex é a sexta, 02/10.
        var datas = AgendaDeSessoes.GerarQuantidade(new DateOnly(2026, 10, 1), Segunda | Quarta | Sexta, 6);

        Assert.Equal(
            [new(2026, 10, 2), new(2026, 10, 5), new(2026, 10, 7), new(2026, 10, 9), new(2026, 10, 12), new(2026, 10, 14)],
            datas);
    }

    [Fact]
    public void Inicio_num_dia_marcado_conta_como_primeira_sessao()
    {
        var datas = AgendaDeSessoes.GerarQuantidade(new DateOnly(2026, 10, 5), Segunda, 2);
        Assert.Equal([new(2026, 10, 5), new(2026, 10, 12)], datas);
    }

    [Fact]
    public void N_sessoes_atravessam_a_virada_do_ano()
    {
        // 28/12/2026 é segunda-feira.
        var datas = AgendaDeSessoes.GerarQuantidade(new DateOnly(2026, 12, 28), Terca | Quinta, 4);
        Assert.Equal([new(2026, 12, 29), new(2026, 12, 31), new(2027, 1, 5), new(2027, 1, 7)], datas);
    }

    [Theory]
    [InlineData("2026-09-29", "2026-10-31")]
    [InlineData("2026-12-15", "2027-01-31")]
    [InlineData("2027-01-31", "2027-02-28")]
    [InlineData("2027-11-01", "2027-12-31")]
    public void Horizonte_do_continuo_e_o_fim_do_mes_seguinte(string hoje, string esperado)
    {
        Assert.Equal(DateOnly.Parse(esperado), AgendaDeSessoes.FimDoMesSeguinte(DateOnly.Parse(hoje)));
    }

    [Fact]
    public void Continuo_que_comeca_mais_adiante_conta_o_horizonte_a_partir_do_inicio()
    {
        var horizonte = AgendaDeSessoes.HorizonteContinuo(new DateOnly(2026, 12, 10), new DateOnly(2026, 9, 29));
        Assert.Equal(new DateOnly(2027, 1, 31), horizonte);
    }

    [Fact]
    public void Gerar_ate_inclui_as_duas_pontas()
    {
        var datas = AgendaDeSessoes.GerarAte(new DateOnly(2026, 10, 5), Segunda | Sexta, new DateOnly(2026, 10, 16));
        Assert.Equal([new(2026, 10, 5), new(2026, 10, 9), new(2026, 10, 12), new(2026, 10, 16)], datas);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(128, 3)]
    [InlineData(Segunda, 0)]
    [InlineData(Segunda, 366)]
    public void Mascara_ou_quantidade_fora_da_faixa_nao_gera_nada(int mascara, int quantidade)
    {
        Assert.Empty(AgendaDeSessoes.GerarQuantidade(new DateOnly(2026, 10, 1), mascara, quantidade));
    }
}
