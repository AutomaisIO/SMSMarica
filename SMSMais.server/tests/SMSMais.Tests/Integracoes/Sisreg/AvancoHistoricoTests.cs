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

    private static void SemearExecucao(
        SmsMaisDbContext db, Guid unidadeId, DateOnly inicio, DateOnly fim,
        StatusVarredura status, int registros)
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
            IniciadoEm = DateTime.UtcNow,
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
        var agenda = await SemearAsync(db);

        var (inicio, fim) = DecididorHistorico.ProximaFatia(null, Hoje, 31);
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.EmExecucao, registros: 4272);
        await db.SaveChangesAsync();

        // Com trabalho vivo de verdade, esperar é o certo.
        var esperando = await AvancoHistorico.ReconciliarAsync(
            db, agenda, Hoje, new HistoricoOpcoes(), default, nenhumTrabalhoVivo: false);
        Assert.Equal(PassoHistorico.Esperar, esperando.Passo);

        // Sem nada rodando, a mesma linha só pode ser órfã.
        var orfa = await AvancoHistorico.ReconciliarAsync(
            db, agenda, Hoje, new HistoricoOpcoes(), default, nenhumTrabalhoVivo: true);

        Assert.Equal(PassoHistorico.Repetir, orfa.Passo);
        Assert.Equal(inicio, orfa.Inicio);
        Assert.Null(agenda.HistoricoCobertoDe);
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
        SemearExecucao(db, agenda.UnidadeId, inicio, fim, StatusVarredura.Erro, registros: 0);
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
