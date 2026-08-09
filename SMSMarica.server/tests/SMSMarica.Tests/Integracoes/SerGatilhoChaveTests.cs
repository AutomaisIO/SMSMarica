using FluentAssertions;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// A chave do gatilho é o que o índice único <c>(solicitação, tipo, chave)</c> compara.
///
/// <para><b>Regressão de 08/08/2026:</b> a chave de mudança de situação era só o DESTINO. A
/// segunda vez que a mesma solicitação fosse cancelada — ciclo <c>Cancelada → Em fila →
/// Cancelada</c>, que o SER permite — repetia a chave, estourava o índice e derrubava o
/// <c>SaveChanges</c> do lote inteiro. E cada volta é uma notificação legítima: o operador precisa
/// ver as duas.</para>
/// </summary>
public class SerGatilhoChaveTests
{
    /// <summary>Espelha o formato usado em <c>SerSincronizacaoService</c>.</summary>
    private static string ChaveSituacao(SituacaoSer de, SituacaoSer para, DateTime quando) =>
        $"{de}>{para}@{quando:yyyyMMddHHmmss}";

    [Fact]
    public void Mesma_transicao_em_momentos_diferentes_gera_chaves_diferentes()
    {
        var primeira = ChaveSituacao(
            SituacaoSer.EmFila, SituacaoSer.Cancelada, new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc));
        var segunda = ChaveSituacao(
            SituacaoSer.EmFila, SituacaoSer.Cancelada, new DateTime(2026, 8, 9, 22, 30, 0, DateTimeKind.Utc));

        segunda.Should().NotBe(primeira, "cancelar duas vezes são dois avisos, não um duplicado");
    }

    /// <summary>A chave carrega a ORIGEM também: sair de "Em fila" e sair de "Agendada" para
    /// Cancelada são movimentos diferentes, e a tela mostra a transição inteira.</summary>
    [Fact]
    public void Transicoes_com_origens_diferentes_nao_colidem()
    {
        var quando = new DateTime(2026, 8, 9, 22, 30, 0, DateTimeKind.Utc);

        ChaveSituacao(SituacaoSer.EmFila, SituacaoSer.Cancelada, quando)
            .Should().NotBe(ChaveSituacao(SituacaoSer.Agendada, SituacaoSer.Cancelada, quando));
    }

    /// <summary>
    /// O mesmo movimento detectado duas vezes no MESMO instante continua sendo um só — é o que
    /// preserva a idempotência que o índice único existe para garantir.
    /// </summary>
    [Fact]
    public void Mesma_transicao_no_mesmo_instante_continua_sendo_uma_chave_so()
    {
        var quando = new DateTime(2026, 8, 9, 22, 30, 0, DateTimeKind.Utc);

        ChaveSituacao(SituacaoSer.EmFila, SituacaoSer.Cancelada, quando)
            .Should().Be(ChaveSituacao(SituacaoSer.EmFila, SituacaoSer.Cancelada, quando));
    }

    /// <summary>A coluna é <c>varchar(80)</c>. A transição mais longa que existe tem de caber —
    /// estourar aqui derrubaria a rodada como o agendamento derrubou em 08/08.</summary>
    [Fact]
    public void Chave_da_transicao_mais_longa_cabe_na_coluna()
    {
        var chave = ChaveSituacao(
            SituacaoSer.ChegadaNaoConfirmada, SituacaoSer.ChegadaConfirmada, DateTime.UtcNow);

        chave.Length.Should().BeLessThanOrEqualTo(80);
    }
}
