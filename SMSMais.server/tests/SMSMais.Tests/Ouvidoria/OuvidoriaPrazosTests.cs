using SMSMais.Core.Ouvidoria;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Tests.Ouvidoria;

/// <summary>
/// Cálculo puro de prazos (plano §2.10, D-4). Sem banco e sem relógio: cada teste passa o "hoje".
/// Regressões que estes testes impedem: prazo da área ignorando o prazo próprio do ponto na
/// prioridade Normal; Urgente contado em dias corridos (a lei conta em dias úteis); atraso
/// negativo poluindo o painel; faixas do painel erradas nos limites 30/60.
/// </summary>
public class OuvidoriaPrazosTests
{
    private static OuvidoriaConfiguracao Config() => new()
    {
        PrazoCidadaoDias = 30,
        ProrrogacaoDias = 30,
        PrazoAreaDias = 20,
        PrazoAreaAltaDias = 10,
        PrazoAreaUrgenteDiasUteis = 2,
    };

    [Fact]
    public void Prazo_cidadao_soma_dias_corridos_ao_registro()
    {
        var registro = new DateOnly(2026, 9, 1);

        var prazo = OuvidoriaPrazos.PrazoCidadao(registro, 30);

        Assert.Equal(new DateOnly(2026, 10, 1), prazo);
    }

    [Fact]
    public void Prazo_area_normal_usa_prazo_do_ponto_quando_informado()
    {
        var hoje = new DateOnly(2026, 9, 14); // segunda

        var prazo = OuvidoriaPrazos.PrazoArea(hoje, OuvidoriaPrioridade.Normal, Config(), prazoDoPonto: 5);

        Assert.Equal(hoje.AddDays(5), prazo);
    }

    [Fact]
    public void Prazo_area_normal_sem_prazo_do_ponto_usa_configuracao()
    {
        var hoje = new DateOnly(2026, 9, 14);

        var prazo = OuvidoriaPrazos.PrazoArea(hoje, OuvidoriaPrioridade.Normal, Config(), prazoDoPonto: null);

        Assert.Equal(hoje.AddDays(20), prazo);
    }

    /// <summary>Alta ignora o prazo do ponto: a prioridade é da ouvidoria, não da área.</summary>
    [Fact]
    public void Prazo_area_alta_usa_configuracao_e_ignora_prazo_do_ponto()
    {
        var hoje = new DateOnly(2026, 9, 14);

        var prazo = OuvidoriaPrazos.PrazoArea(hoje, OuvidoriaPrioridade.Alta, Config(), prazoDoPonto: 40);

        Assert.Equal(hoje.AddDays(10), prazo);
    }

    /// <summary>Sexta + 2 dias úteis = terça: sábado e domingo não contam.</summary>
    [Fact]
    public void Prazo_area_urgente_conta_dias_uteis_pulando_fim_de_semana()
    {
        var sexta = new DateOnly(2026, 9, 18);
        Assert.Equal(DayOfWeek.Friday, sexta.DayOfWeek);

        var prazo = OuvidoriaPrazos.PrazoArea(sexta, OuvidoriaPrioridade.Urgente, Config(), prazoDoPonto: 1);

        Assert.Equal(new DateOnly(2026, 9, 22), prazo);
        Assert.Equal(DayOfWeek.Tuesday, prazo.DayOfWeek);
    }

    [Fact]
    public void Dias_atraso_nunca_e_negativo()
    {
        var prazo = new DateOnly(2026, 9, 30);

        Assert.Equal(0, OuvidoriaPrazos.DiasAtraso(prazo, new DateOnly(2026, 9, 10)));
        Assert.Equal(0, OuvidoriaPrazos.DiasAtraso(prazo, prazo));
        Assert.Equal(3, OuvidoriaPrazos.DiasAtraso(prazo, new DateOnly(2026, 10, 3)));
    }

    [Theory]
    [InlineData(0, "ate30")]
    [InlineData(30, "ate30")]
    [InlineData(31, "31a60")]
    [InlineData(60, "31a60")]
    [InlineData(61, "mais60")]
    [InlineData(200, "mais60")]
    public void Faixa_prazo_respeita_os_limites_30_e_60(int dias, string esperado)
    {
        Assert.Equal(esperado, OuvidoriaPrazos.FaixaPrazo(dias));
    }

    [Fact]
    public void Adicionar_dias_uteis_pula_sabado_e_domingo()
    {
        var sexta = new DateOnly(2026, 9, 18);
        var sabado = new DateOnly(2026, 9, 19);

        Assert.Equal(new DateOnly(2026, 9, 21), OuvidoriaPrazos.AdicionarDiasUteis(sexta, 1));  // segunda
        Assert.Equal(new DateOnly(2026, 9, 21), OuvidoriaPrazos.AdicionarDiasUteis(sabado, 1)); // segunda
        Assert.Equal(new DateOnly(2026, 9, 25), OuvidoriaPrazos.AdicionarDiasUteis(sexta, 5));  // sexta seguinte
    }

    [Fact]
    public void Adicionar_zero_ou_negativo_devolve_a_propria_data()
    {
        var data = new DateOnly(2026, 9, 18);

        Assert.Equal(data, OuvidoriaPrazos.AdicionarDiasUteis(data, 0));
        Assert.Equal(data, OuvidoriaPrazos.AdicionarDiasUteis(data, -3));
    }
}
