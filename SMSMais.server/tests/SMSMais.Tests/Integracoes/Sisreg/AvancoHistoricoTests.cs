using SMSMais.Core.Integracoes.SisregWeb.Historico;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A reconciliação que move a cobertura do histórico — e o defeito que ela existe para não repetir.
///
/// <para><b>O incidente (05/09/2026, CDT).</b> O comando manual de "avançar o passado" sabia
/// <i>começar</i> uma fatia, mas quem gravava <c>HistoricoCobertoDe</c> era só o scheduler — que
/// para quando o sincronismo automático está desligado. Resultado em produção: duas execuções
/// manuais na <b>mesma</b> janela (05/08 a 04/09), a primeira trazendo 3.923 registros e a segunda
/// reimportando tudo, com a cobertura eternamente nula. Cada clique gastava requisição do orçamento
/// anti-robô para rebuscar o que já estava no banco, e a tela seguia dizendo "coberto até: só o que
/// a varredura diária trouxe".</para>
///
/// <para>Por isso a regra passou a viver em um lugar só (<see cref="AvancoHistorico"/>), chamado
/// pelos dois caminhos: o que estes testes prendem é que reconciliar <b>avança</b> e que a fatia
/// seguinte é <b>outra</b>.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AvancoHistoricoTests(PostgresFixture fixture)
{
    private static readonly DateOnly Hoje = new(2026, 9, 5);

    private static async Task<SisregVarreduraAgenda> SemearAsync(
        SmsMaisDbContext db, DateOnly? cobertoDe = null, int fatiasVazias = 0)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE HIST {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        var agenda = new SisregVarreduraAgenda
        {
            UnidadeId = unidade.Id,
            Ativo = false,
            HistoricoAtivo = true,
            HistoricoCobertoDe = cobertoDe,
            HistoricoFatiasVazias = fatiasVazias,
        };
        db.Unidades.Add(unidade);
        db.SisregVarreduraAgendas.Add(agenda);
        await db.SaveChangesAsync();
        return agenda;
    }

    /// <param name="minutosAtras">Idade da execução — é o que distingue "rodando" de "abandonada".</param>
    private static void SemearExecucao(
        SmsMaisDbContext db, Guid unidadeId, DateOnly inicio, DateOnly fim,
        StatusVarredura status, int registros, int minutosAtras = 0)
    {
        db.SisregVarreduraExecucoes.Add(new SisregVarreduraExecucao
        {
            Id = Guid.NewGuid(),
            UnidadeId = unidadeId,
            UnidadeNome = "UNIDADE HIST",
            Disparo = DisparoSincronizacao.Manual,
            Status = status,
            JanelaInicio = inicio,
            JanelaFim = fim,
            RegistrosEncontrados = registros,
            IniciadoEm = DateTime.UtcNow.AddMinutes(-minutosAtras),
        });
    }

    /// <summary>
    /// Fatia concluída com registros <b>move</b> a cobertura, e a próxima janela é outra.
    ///
    /// <para>É exatamente o que faltava: sem o avanço, a fatia seguinte calculada a partir de uma
    /// cobertura nula é sempre a mesma, e o operador reimporta o mesmo mês a cada clique.</para>
    /// </summary>
    [Fact]
    public async Task Fatia_concluida_avanca_a_cobertura_e_a_proxima_janela_e_outra()
    {
        await using var db = fixture.CriarDbContext();
        var agenda = await SemearAsync(db);

        // A primeira fatia a partir de 05/09 é 05/08–04/09.
        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, 31);
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Concluida, registros: 3923);
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, new HistoricoOpcoes(), default);

        Assert.Equal(PassoHistorico.Avancar, r.Passo);
        Assert.Equal(inicio, agenda.HistoricoCobertoDe);
        Assert.Equal(0, agenda.HistoricoFatiasVazias);

        // A janela devolvida tem de ser a ANTERIOR, nunca a que acabou de rodar.
        Assert.NotEqual(inicio, r.Inicio);
        Assert.Equal(inicio.AddDays(-1), r.Fim);
    }

    /// <summary>
    /// Fatia ainda rodando não move nada — e devolve a MESMA janela, para o chamador esperar.
    ///
    /// <para>Avançar sobre uma fatia em andamento deixaria um buraco: a tela diria "coberto desde X"
    /// com o dado ainda a caminho, e ninguém teria como saber que ele ficou faltando.</para>
    /// </summary>
    [Fact]
    public async Task Fatia_em_andamento_nao_move_a_cobertura()
    {
        await using var db = fixture.CriarDbContext();
        var agenda = await SemearAsync(db);

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, 31);
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.EmExecucao, registros: 0);
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, new HistoricoOpcoes(), default);

        Assert.Equal(PassoHistorico.Esperar, r.Passo);
        Assert.Null(agenda.HistoricoCobertoDe);
        Assert.Equal(inicio, r.Inicio);
    }

    /// <summary>
    /// Execução ÓRFÃ de um restart é repetida, em vez de travar o motor para sempre.
    ///
    /// <para>O estado "rodando" vive em memória; o banco, não. Um deploy no meio de uma fatia deixa
    /// a linha eternamente em <c>EmExecucao</c> — aconteceu em 06/09/2026 no CDT, com 21
    /// requisições e 4.272 registros lidos e nenhum <c>finalizado_em</c>. Como <c>Decidir</c>
    /// devolve <c>Esperar</c> para "rodando", o histórico ficaria parado naquela janela
    /// indefinidamente, sem nada na tela explicando por quê.</para>
    /// </summary>
    [Fact]
    public async Task Execucao_orfa_de_restart_e_repetida_e_nao_trava_o_motor()
    {
        await using var db = fixture.CriarDbContext();
        var opcoes = new HistoricoOpcoes();
        var agenda = await SemearAsync(db);
        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, opcoes.DiasPorFatia);

        // Recém-criada: está mesmo rodando, e esperar é o certo. É este caso que o laço de
        // 06/09/2026 lia como órfã, disparando outra execução a cada 90 s.
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.EmExecucao, 4272, minutosAtras: 1);
        await db.SaveChangesAsync();

        var recente = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, opcoes, default);
        Assert.Equal(PassoHistorico.Esperar, recente.Passo);

        // Velha demais para estar viva: aí sim é resto de um restart.
        await using var db2 = fixture.CriarDbContext();
        var agenda2 = await SemearAsync(db2);
        SemearExecucao(db2, agenda2.UnidadeId, inicio, fim, StatusVarredura.EmExecucao, 4272,
            minutosAtras: opcoes.MinutosParaAbandonada + 5);
        await db2.SaveChangesAsync();

        var velha = await AvancoHistorico.ReconciliarAsync(db2, agenda2, Hoje, opcoes, default);
        Assert.Equal(PassoHistorico.Repetir, velha.Passo);
        Assert.Null(agenda2.HistoricoCobertoDe);
    }

    /// <summary>
    /// A mesma fatia falhando vezes demais DESLIGA o histórico, em vez de insistir.
    ///
    /// <para>É o teto que faltava: sem ele, uma janela que nunca passa repete para sempre e queima
    /// orçamento anti-robô — cujo estouro pausa a unidade por 24 h.</para>
    /// </summary>
    [Fact]
    public async Task Fatia_que_falha_demais_desliga_o_historico()
    {
        await using var db = fixture.CriarDbContext();
        var opcoes = new HistoricoOpcoes();
        var agenda = await SemearAsync(db);
        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, opcoes.DiasPorFatia);

        for (var i = 0; i < opcoes.TentativasPorFatia; i++)
        {
            SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Erro, 0, minutosAtras: 60 + i);
        }
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, opcoes, default);

        Assert.Equal(PassoHistorico.Bloquear, r.Passo);
        Assert.False(agenda.HistoricoAtivo);
    }

    /// <summary>
    /// Uma execução CONCLUÍDA vence a mais recente na mesma janela.
    ///
    /// <para>"Esta janela está coberta?" se responde por ter dado certo alguma vez, não pela última
    /// tentativa. No CDT, em 06/09/2026, a janela 05/08–04/09 concluiu com 3.923 registros e depois
    /// ganhou uma re-execução que virou órfã num deploy; sem esta ordenação, o motor gastaria 21
    /// requisições e 22 minutos rebuscando dado que já estava no banco.</para>
    /// </summary>
    [Fact]
    public async Task Execucao_concluida_vence_a_mais_recente_da_mesma_janela()
    {
        await using var db = fixture.CriarDbContext();
        var agenda = await SemearAsync(db);

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, 31);

        // Primeiro concluiu com dados; depois uma re-execução ficou órfã de um restart.
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Concluida, registros: 3923);
        await db.SaveChangesAsync();
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.EmExecucao, registros: 4272);
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(
            db, agenda, Hoje, new HistoricoOpcoes(), default);

        Assert.Equal(PassoHistorico.Avancar, r.Passo);
        Assert.Equal(inicio, agenda.HistoricoCobertoDe);
    }

    /// <summary>
    /// Fatia que terminou mal é <b>repetida</b>, sem avançar a cobertura.
    ///
    /// <para>Repetir custa uma requisição; avançar por cima de uma falha custa a análise inteira, e
    /// o buraco seria invisível.</para>
    /// </summary>
    [Fact]
    public async Task Fatia_com_falha_repete_a_mesma_janela()
    {
        await using var db = fixture.CriarDbContext();
        var agenda = await SemearAsync(db);

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, 31);
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Erro, registros: 0,
            minutosAtras: new HistoricoOpcoes().MinutosEntreTentativas + 1);
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, new HistoricoOpcoes(), default);

        Assert.Equal(PassoHistorico.Repetir, r.Passo);
        Assert.Null(agenda.HistoricoCobertoDe);
        Assert.Equal(inicio, r.Inicio);
        Assert.Equal(fim, r.Fim);
    }

    /// <summary>
    /// Chegar ao limite de fatias vazias encerra a unidade — é assim que o motor descobre onde ela
    /// começou, em vez de confiar na data de cadastro do SISREG.
    /// </summary>
    [Fact]
    public async Task Ultima_fatia_vazia_conclui_a_unidade()
    {
        await using var db = fixture.CriarDbContext();
        var opcoes = new HistoricoOpcoes();
        // Uma vazia a menos do que o limite: a que vem agora fecha a conta.
        var agenda = await SemearAsync(db, fatiasVazias: opcoes.FatiasVaziasParaConcluir - 1);

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, opcoes.DiasPorFatia);
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Concluida, registros: 0);
        await db.SaveChangesAsync();

        var r = await AvancoHistorico.ReconciliarAsync(db, agenda, Hoje, opcoes, default);

        Assert.Equal(PassoHistorico.Concluir, r.Passo);
        Assert.NotNull(agenda.HistoricoConcluidoEm);
        Assert.Equal(inicio, agenda.HistoricoCobertoDe);
    }
}
