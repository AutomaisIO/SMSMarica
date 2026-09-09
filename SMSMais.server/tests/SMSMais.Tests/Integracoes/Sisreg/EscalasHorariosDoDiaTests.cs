using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb.Escalas;
using SMSMais.Core.Integracoes.SisregWeb.Escalas.Dtos;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Vários horários por dia no sincronismo de escalas.
///
/// <para><b>Por que isto existe.</b> A escala é a OFERTA: um bloco novo ("Gastro abriu 20 vagas no
/// Conde") nasce no SISREG a qualquer hora. Com um disparo diário de madrugada, uma agenda aberta
/// às 09:00 só era vista 17 horas depois — e vaga que ninguém sabe que existe não é aproveitada.
/// Sincronizar de novo custa <b>1 requisição para a rede inteira</b>, e o <c>cons_escalas</c> não
/// sofre a trava 07:30–15:00 do <c>expo_solicitacoes</c>, então 06/12/18 é possível aqui e não
/// seria na varredura de agenda.</para>
///
/// <para>O que se testa é a leitura e a normalização da configuração — a parte que decide QUANDO
/// disparar e que, errada, faz o motor rodar uma vez só ou nenhuma.</para>
/// </summary>
public class EscalasHorariosDoDiaTests
{
    private static SalvarEscalasAgendamentoRequest Pedido(params string[] horarios) =>
        new(Ativo: true, HoraLocal: null, HorariosLocais: horarios);

    [Fact]
    public void Tres_horarios_do_dia_sobrevivem_ida_e_volta()
    {
        var salvos = EscalasSincronizacaoService.NormalizarHorarios(Pedido("06:00", "12:00", "18:00"));

        var json = new JsonObject
        {
            [EscalasSincronizacaoService.ChaveHorarios] = new JsonArray([.. salvos.Select(h => JsonValue.Create(h))]),
        };

        Assert.Equal(["06:00", "12:00", "18:00"], EscalasSincronizacaoService.LerHorarios(json, "02:30"));
    }

    [Fact]
    public void Horarios_saem_ordenados_e_sem_repeticao()
    {
        // Fora de ordem e com duplicata: o scheduler percorre a lista procurando o slot vencido
        // mais recente, e ordem embaralhada faria ele eleger o slot errado.
        var r = EscalasSincronizacaoService.NormalizarHorarios(Pedido("18:00", "06:00", "12:00", "06:00"));

        Assert.Equal(["06:00", "12:00", "18:00"], r);
    }

    [Fact]
    public void Configuracao_antiga_de_um_horario_so_continua_valendo()
    {
        // Quem já tinha `escalasHoraLocal` não pode virar "padrão de fábrica" no deploy.
        var json = new JsonObject { [EscalasSincronizacaoService.ChaveHora] = "03:15" };

        Assert.Equal(["03:15"], EscalasSincronizacaoService.LerHorarios(json, "02:30"));
    }

    [Fact]
    public void Sem_configuracao_nenhuma_cai_no_padrao()
    {
        Assert.Equal(["02:30"], EscalasSincronizacaoService.LerHorarios(null, "02:30"));
        Assert.Equal(["02:30"], EscalasSincronizacaoService.LerHorarios(new JsonObject(), "02:30"));
    }

    [Fact]
    public void Lista_nova_tem_precedencia_sobre_o_campo_antigo()
    {
        var json = new JsonObject
        {
            [EscalasSincronizacaoService.ChaveHora] = "02:30",
            [EscalasSincronizacaoService.ChaveHorarios] = new JsonArray("06:00", "18:00"),
        };

        Assert.Equal(["06:00", "18:00"], EscalasSincronizacaoService.LerHorarios(json, "02:30"));
    }

    [Fact]
    public void Item_invalido_no_json_nao_derruba_os_horarios_validos()
    {
        // JSON editado à mão não pode impedir o motor de rodar nos horários que estão certos.
        var json = new JsonObject
        {
            [EscalasSincronizacaoService.ChaveHorarios] = new JsonArray("06:00", "banana", "18:00"),
        };

        Assert.Equal(["06:00", "18:00"], EscalasSincronizacaoService.LerHorarios(json, "02:30"));
    }

    [Fact]
    public void Lista_toda_invalida_cai_no_padrao_em_vez_de_devolver_vazio()
    {
        // O scheduler indexa o primeiro elemento; lista vazia o derrubaria a cada tick.
        var json = new JsonObject
        {
            [EscalasSincronizacaoService.ChaveHorarios] = new JsonArray("banana", "25:99"),
        };

        var r = EscalasSincronizacaoService.LerHorarios(json, "02:30");

        Assert.Single(r);
        Assert.Equal("02:30", r[0]);
    }

    [Fact]
    public void Pedido_com_o_campo_antigo_ainda_e_aceito()
    {
        var r = EscalasSincronizacaoService.NormalizarHorarios(
            new SalvarEscalasAgendamentoRequest(Ativo: true, HoraLocal: "04:45"));

        Assert.Equal(["04:45"], r);
    }

    [Fact]
    public void Mais_horarios_que_o_teto_e_recusado_com_o_motivo()
    {
        var ex = Assert.Throws<ValidacaoException>(() =>
            EscalasSincronizacaoService.NormalizarHorarios(
                Pedido("01:00", "02:00", "03:00", "04:00", "05:00", "06:00", "07:00")));

        Assert.True(ex.Erros.ContainsKey("escalas.horarios_demais"));
    }

    [Fact]
    public void Hora_invalida_no_pedido_e_recusada()
    {
        Assert.Throws<ValidacaoException>(() => EscalasSincronizacaoService.NormalizarHorarios(Pedido("26:00")));
        Assert.Throws<ValidacaoException>(() => EscalasSincronizacaoService.NormalizarHorarios(Pedido("")));
    }
}
