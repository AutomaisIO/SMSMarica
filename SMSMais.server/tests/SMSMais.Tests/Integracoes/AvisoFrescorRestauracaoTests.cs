using FluentAssertions;
using SMSMais.Core.Integracoes.Sisreg.Base;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O aviso de base desatualizada depois de um reinício do servidor. Caso real: 09/10/2026 o resumo do
/// dia saiu às 07:00 e foi repetido às 08:28 e às 11:01, os dois por deploy — o que já tinha sido
/// avisado vivia só em memória.
/// </summary>
public class AvisoFrescorRestauracaoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 9);

    // 07:00 BRT = 10:00 UTC.
    private static readonly DateTime HojeSeteDaManha = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OntemAsSeis = new(2026, 10, 8, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sem_aviso_hoje_e_a_primeira_do_dia() =>
        AvisoFrescorBaseSisregWorker.RestaurarEstado(OntemAsSeis, null, Hoje)
            .Should().Be(new AvisoFrescorBaseSisregWorker.EstadoRestaurado(false, false));

    [Fact]
    public void Desatualizada_hoje_nao_repete_o_resumo_nem_as_pendencias() =>
        AvisoFrescorBaseSisregWorker.RestaurarEstado(HojeSeteDaManha, null, Hoje)
            .Should().Be(new AvisoFrescorBaseSisregWorker.EstadoRestaurado(true, true));

    [Fact]
    public void Normalizou_depois_do_aviso_resumo_feito_e_nada_pendente_avisado() =>
        AvisoFrescorBaseSisregWorker.RestaurarEstado(HojeSeteDaManha, HojeSeteDaManha.AddHours(3), Hoje)
            .Should().Be(new AvisoFrescorBaseSisregWorker.EstadoRestaurado(true, false));

    [Fact]
    public void Voltou_a_desatualizar_depois_de_normalizar_conta_como_avisada() =>
        AvisoFrescorBaseSisregWorker.RestaurarEstado(HojeSeteDaManha.AddHours(5), HojeSeteDaManha.AddHours(3), Hoje)
            .Should().Be(new AvisoFrescorBaseSisregWorker.EstadoRestaurado(true, true));

    [Fact]
    public void Dia_de_hoje_e_o_de_Brasilia_nao_o_de_UTC()
    {
        // 23:30 BRT de 08/10 = 02:30 UTC de 09/10: em Brasília ainda era ontem.
        var ontemTardeDaNoite = new DateTime(2026, 10, 9, 2, 30, 0, DateTimeKind.Utc);
        AvisoFrescorBaseSisregWorker.RestaurarEstado(ontemTardeDaNoite, null, Hoje).ResumoJaMandado.Should().BeFalse();
    }
}
