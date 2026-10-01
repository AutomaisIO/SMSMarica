using System.Text.Json;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Tests.EsusSg;

/// <summary>
/// Leitura do comparecimento no ESUS SG, sem banco e sem ESUS: os JSON abaixo têm a forma medida em
/// 01/10/2026 (dados sintéticos). O que se protege: o corpo do histórico (é <c>arrFiltro</c>, e chave
/// errada no legado dá 0 linhas calado), a trava de leitura deixar passar as duas telas, e a regra de
/// "vale o último apontamento".
/// </summary>
public class EsusSgEfetivacaoTests
{
    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement.Clone();

    [Fact]
    public void Historico_so_traz_exames_e_separa_os_ids()
    {
        var exame = EsusSgHistoricoExame.De(Json(
            """{"id":"78090,78093","id_fila":"154485","id_modulo":"33","data_agendamento":"11/05/2019","nome_modulo":"EXAMES"}"""));
        Assert.NotNull(exame);
        Assert.Equal([78090L, 78093L], exame!.IdsExame);
        Assert.Equal(new DateOnly(2019, 5, 11), exame.DataAgendamento);

        Assert.Null(EsusSgHistoricoExame.De(Json("""{"id":"5","id_modulo":"30","data_agendamento":"11/05/2019"}""")));
        Assert.Null(EsusSgHistoricoExame.De(Json("""{"id":"","id_modulo":"33"}""")));
    }

    [Fact]
    public void Trilha_le_efetivacao_motivo_e_datas_com_traco_em_horario_de_Brasilia()
    {
        var e = EsusSgEventoExame.De(Json("""
            {"@rownum := @rownum+1":"4","data_hora_log":"09/09/2026 - 11:33","tlg_nome":"NÃO EFETIVADO",
             "efl_id_exames_efetivacao":"3","data_efetivacao":"","motivo_nao_efetivacao":"Não Compareceu",
             "data_exame":"09/09/2026 ","fil_id":"4064691"}
            """));
        Assert.Equal(EfetivacaoEsusSg.NaoEfetivado, e.Efetivacao);
        Assert.Equal("Não Compareceu", e.MotivoNaoEfetivacao);
        Assert.Equal("4064691", e.FilId);
        Assert.Equal(4, e.Ordem);
        Assert.Equal(FusoBrasilia.DeBrasiliaParaUtc(new DateTime(2026, 9, 9, 11, 33, 0)), e.Registro);
        Assert.Null(e.EfetivadoEm);

        var ok = EsusSgEventoExame.De(Json("""
            {"data_hora_log":"16/06/2026 - 15:42","efl_id_exames_efetivacao":"2","data_efetivacao":"16/06/2026 - 15:42:10"}
            """));
        Assert.Equal(EfetivacaoEsusSg.Efetivado, ok.Efetivacao);
        Assert.Equal(FusoBrasilia.DeBrasiliaParaUtc(new DateTime(2026, 6, 16, 15, 42, 10)), ok.EfetivadoEm);

        // Evento sem apontamento (AGENDADO) e código fora do domínio não viram estado.
        Assert.Null(EsusSgEventoExame.De(Json("""{"tlg_nome":"AGENDADO","efl_id_exames_efetivacao":""}""")).Efetivacao);
        Assert.Null(EsusSgEventoExame.De(Json("""{"efl_id_exames_efetivacao":"7"}""")).Efetivacao);
    }

    private static EsusSgEventoExame Ev(EfetivacaoEsusSg? e, int minuto, int ordem = 0, string? motivo = null) =>
        new("1", null, e, null, motivo, new DateTime(2026, 9, 9, 12, minuto, 0, DateTimeKind.Utc), null, ordem);

    [Fact]
    public void Vale_o_ultimo_apontamento_e_um_exame_efetivado_basta()
    {
        // Não efetivado e depois corrigido para efetivado: vale o efetivado.
        var r = EfetivacaoEsusSgResolvida.Resolver([[Ev(null, 0), Ev(EfetivacaoEsusSg.NaoEfetivado, 10, motivo: "Não Compareceu"),
            Ev(EfetivacaoEsusSg.Efetivado, 20)]]);
        Assert.Equal(EfetivacaoEsusSg.Efetivado, r.Estado);
        Assert.Null(r.Motivo);

        // Mesmo minuto: desempata pela ordem do ESUS.
        r = EfetivacaoEsusSgResolvida.Resolver([[Ev(EfetivacaoEsusSg.Efetivado, 5, 1), Ev(EfetivacaoEsusSg.EmAberto, 5, 2)]]);
        Assert.Equal(EfetivacaoEsusSg.EmAberto, r.Estado);

        // Dois exames no mesmo agendamento: um efetivado basta.
        r = EfetivacaoEsusSgResolvida.Resolver([[Ev(EfetivacaoEsusSg.NaoEfetivado, 30, motivo: "Não Compareceu")],
            [Ev(EfetivacaoEsusSg.Efetivado, 1)]]);
        Assert.Equal(EfetivacaoEsusSg.Efetivado, r.Estado);

        // Só não efetivado: leva o motivo.
        r = EfetivacaoEsusSgResolvida.Resolver([[Ev(EfetivacaoEsusSg.NaoEfetivado, 1, motivo: "Não Compareceu")]]);
        Assert.Equal("Não Compareceu", r.Motivo);

        Assert.Equal(EfetivacaoEsusSgResolvida.Nada, EfetivacaoEsusSgResolvida.Resolver([[Ev(null, 0)], []]));
    }

    [Fact]
    public void Corpo_do_historico_e_arrFiltro_e_as_duas_telas_passam_na_trava_de_leitura()
    {
        var corpo = EsusSgLeitorService.FormHistorico("123456", new DateOnly(2026, 10, 1));
        Assert.NotNull(corpo["arrFiltro"]);
        Assert.Null(corpo["arrFormData"]);
        Assert.Equal(123456, (int)corpo["arrFiltro"]!["pes_id"]!);
        Assert.Equal("01/10/2026", (string)corpo["arrFiltro"]!["periodoFinal"]!);
        Assert.Throws<FormatException>(() => EsusSgLeitorService.FormHistorico("p123", new DateOnly(2026, 10, 1)));

        EsusSgSessao.GarantirLeitura("pacientes/controller-paciente/buscar-historico-geral-paciente");
        EsusSgSessao.GarantirLeitura("pacientes/controller-paciente/buscar-detalhes-historico-exame-paciente");
    }
}
