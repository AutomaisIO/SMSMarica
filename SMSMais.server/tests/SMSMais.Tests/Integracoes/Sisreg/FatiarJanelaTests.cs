using SMSMais.Core.Integracoes.SisregWeb.Varredura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Corte da janela de varredura em fatias que o SISREG aceita.
///
/// <para><b>O que mudou e por quê:</b> a varredura lia 21 dias fixos porque cabia numa requisição.
/// Mas a oferta vai muito além — medido em 05/09/2026, a mediana das unidades tem escala até 117
/// dias à frente e o Centro de Radiologia até 2029. Comparar oferta longa com ocupação curta produz
/// <b>disponibilidade fantasma</b>: vaga que aparece livre por falta de dado, e alguém marca em cima
/// de horário já ocupado. Agora a janela vai até a última escala e é lida em fatias.</para>
///
/// <para><b>Por que o corte merece teste próprio:</b> um dia de folga entre fatias vira um dia de
/// agenda que ninguém lê — invisível, todo mês, sem erro nenhum. E sobreposição faz o mesmo
/// agendamento vir duas vezes, inflando a contagem. Os dois erros são silenciosos.</para>
/// </summary>
public class FatiarJanelaTests
{
    private const int MaxDiferenca = VarreduraSisregOpcoes.MaxDiasAFrente;

    [Fact]
    public void Janela_curta_cabe_em_uma_fatia()
    {
        var fatias = VarreduraAgendaService.FatiarJanela(
            new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 25));

        var fatia = Assert.Single(fatias);
        Assert.Equal(new DateOnly(2026, 9, 5), fatia.Inicio);
        Assert.Equal(new DateOnly(2026, 9, 25), fatia.Fim);
    }

    /// <summary>O SISREG recusa exportação com diferença maior que o teto — nenhuma fatia pode passar.</summary>
    [Fact]
    public void Nenhuma_fatia_passa_do_teto_do_sisreg()
    {
        var fatias = VarreduraAgendaService.FatiarJanela(
            new DateOnly(2026, 9, 5), new DateOnly(2029, 9, 23));

        Assert.NotEmpty(fatias);
        Assert.All(fatias, f => Assert.True(f.Fim.DayNumber - f.Inicio.DayNumber <= MaxDiferenca));
    }

    /// <summary>
    /// As fatias encostam sem buraco e sem sobreposição: cada uma começa no dia seguinte ao fim da
    /// anterior. Buraco esconde agenda; sobreposição conta o mesmo agendamento duas vezes.
    /// </summary>
    [Fact]
    public void Fatias_encostam_sem_buraco_e_sem_sobreposicao()
    {
        var fatias = VarreduraAgendaService.FatiarJanela(
            new DateOnly(2026, 9, 5), new DateOnly(2027, 3, 31));

        for (var i = 1; i < fatias.Count; i++)
        {
            Assert.Equal(fatias[i - 1].Fim.AddDays(1), fatias[i].Inicio);
        }
    }

    /// <summary>A união das fatias cobre exatamente a janela pedida — nem um dia a mais, nem a menos.</summary>
    [Fact]
    public void Uniao_das_fatias_cobre_exatamente_a_janela()
    {
        var inicio = new DateOnly(2026, 9, 5);
        var fim = new DateOnly(2027, 3, 31);

        var fatias = VarreduraAgendaService.FatiarJanela(inicio, fim);

        Assert.Equal(inicio, fatias[0].Inicio);
        Assert.Equal(fim, fatias[^1].Fim);
        Assert.Equal(
            fim.DayNumber - inicio.DayNumber + 1,
            fatias.Sum(f => f.Fim.DayNumber - f.Inicio.DayNumber + 1));
    }

    [Fact]
    public void Um_unico_dia_gera_uma_fatia_de_um_dia()
    {
        var dia = new DateOnly(2026, 9, 5);

        var fatia = Assert.Single(VarreduraAgendaService.FatiarJanela(dia, dia));

        Assert.Equal(dia, fatia.Inicio);
        Assert.Equal(dia, fatia.Fim);
    }

    /// <summary>Janela invertida não pode virar laço infinito nem fatia negativa — devolve vazio.</summary>
    [Fact]
    public void Janela_invertida_nao_gera_fatia()
    {
        Assert.Empty(VarreduraAgendaService.FatiarJanela(
            new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 5)));
    }

    /// <summary>
    /// O caso real que motivou tudo: Centro de Radiologia, escala até 23/09/2029. São 36 fatias —
    /// um quarto do custo diário da rede numa unidade só, e é por isso que o número precisa ser
    /// previsível em vez de descoberto em produção.
    /// </summary>
    [Fact]
    public void Centro_de_radiologia_ate_2029_cabe_em_36_fatias()
    {
        var fatias = VarreduraAgendaService.FatiarJanela(
            new DateOnly(2026, 9, 5), new DateOnly(2029, 9, 23));

        Assert.Equal(36, fatias.Count);
    }
}
