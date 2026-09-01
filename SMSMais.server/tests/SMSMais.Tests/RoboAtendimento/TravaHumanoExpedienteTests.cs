using SMSMais.Core.RoboAtendimento.Runtime;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// Expediente humano com DIA DA SEMANA. A regra antiga só olhava a hora: sábado 11h33 contava como
/// "atendente disponível", o prompt afirmava isso ao modelo e, numa simulação sobre caso real, o
/// robô prometeu "vou conectar você agora" — o cidadão esperaria em silêncio até segunda. Estes
/// testes prendem o recorte por dia e preservam o comportamento antigo quando o recorte é nulo.
/// </summary>
public class TravaHumanoExpedienteTests
{
    private static readonly TimeOnly Inicio = new(8, 0);
    private static readonly TimeOnly Fim = new(17, 10);
    private const int SegASex = 62; // bits 1..5

    /// <summary>2026-08-29 foi sábado; 11h33 em Brasília = 14h33 UTC.</summary>
    private static readonly DateTime SabadoDeManhaUtc = new(2026, 8, 29, 14, 33, 0, DateTimeKind.Utc);

    private static readonly DateTime SegundaDeManhaUtc = new(2026, 8, 31, 14, 33, 0, DateTimeKind.Utc);

    [Fact]
    public void Sabado_de_manha_e_FORA_do_expediente_com_recorte_seg_a_sex()
    {
        Assert.True(TravaHumano.ForaDoExpedienteHumano(Inicio, Fim, SabadoDeManhaUtc, SegASex));
    }

    [Fact]
    public void Segunda_de_manha_continua_DENTRO()
    {
        Assert.False(TravaHumano.ForaDoExpedienteHumano(Inicio, Fim, SegundaDeManhaUtc, SegASex));
    }

    [Fact]
    public void Sem_recorte_de_dias_o_comportamento_antigo_permanece()
    {
        // Nulo = todos os dias: sábado de manhã volta a contar como expediente (era o furo, mas é
        // o contrato de quem explicitamente não configurou dias).
        Assert.False(TravaHumano.ForaDoExpedienteHumano(Inicio, Fim, SabadoDeManhaUtc, null));
    }

    [Fact]
    public void Fora_por_hora_continua_valendo_em_dia_util()
    {
        var segundaNoite = new DateTime(2026, 8, 31, 23, 0, 0, DateTimeKind.Utc); // 20h Brasília
        Assert.True(TravaHumano.ForaDoExpedienteHumano(Inicio, Fim, segundaNoite, SegASex));
    }

    [Fact]
    public void Dia_sem_expediente_vence_mesmo_sem_horario_configurado()
    {
        // Só o recorte de dias, sem hora: domingo é fora o dia inteiro.
        var domingoUtc = new DateTime(2026, 8, 30, 15, 0, 0, DateTimeKind.Utc);
        Assert.True(TravaHumano.ForaDoExpedienteHumano(null, null, domingoUtc, SegASex));
    }
}
