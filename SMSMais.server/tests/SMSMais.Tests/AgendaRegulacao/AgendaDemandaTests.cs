using SMSMais.Core.AgendaRegulacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.AgendaRegulacao;

/// <summary>
/// Análise da demanda regulada: volume por procedimento e <b>tempo de espera</b> entre a
/// solicitação e o dia agendado.
///
/// <para>O que está sob teste não é a soma — é a aritmética que só erra em produção, meses depois,
/// quando alguém decide alocar médico com base num número torto. Três armadilhas concretas, todas
/// medidas em dados reais desta base em 05/09/2026:</para>
///
/// <list type="number">
/// <item><b>Fuso.</b> <c>data_agendada</c> é <c>timestamptz</c>. Uma consulta às 22h de Brasília já
/// está no dia seguinte em UTC — o mesmo deslize que, na tela de escalas, produzia 912 contra 934
/// vigentes conforme a hora em que se olhava.</item>
/// <item><b>Espera negativa.</b> Existe solicitação agendada para <i>antes</i> de ter sido pedida.
/// É data errada na origem, não fila de tamanho negativo; se entrar no cálculo puxa a mediana para
/// baixo e melhora o indicador por defeito de dado.</item>
/// <item><b>Mediana, não média.</b> A cauda desta distribuição chega a 1.859 dias. A média
/// descreveria um caso que não é o de ninguém.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgendaDemandaTests(PostgresFixture fixture)
{
    private static AgendaDemandaService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake());

    /// <summary>
    /// Unidade nova a cada teste: a bancada é compartilhada e o serviço, sem usuário no contexto,
    /// enxerga a rede inteira — filtrar pela unidade é o que isola um teste do resíduo do outro.
    /// </summary>
    private static async Task<Guid> UnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE DEMANDA {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private static void Semear(
        SmsMaisDbContext db,
        Guid unidadeId,
        DateTime agendadaUtc,
        DateOnly? solicitacao,
        string procedimento = "PROCEDIMENTO DE TESTE")
    {
        db.Solicitacoes.Add(new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.Imagem,
            UnidadeExecutanteId = unidadeId,
            SolicitanteNome = "DR TESTE",
            ProcedimentoTexto = procedimento,
            Status = StatusSolicitacao.Solicitada,
            StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = DateTime.SpecifyKind(agendadaUtc, DateTimeKind.Utc),
            DataSolicitacao = solicitacao,
            CriadoEm = DateTime.UtcNow,
        });
    }

    private static DemandaFiltro Filtro(Guid unidadeId, string de, string ate, string eixo = "agendada") =>
        new(DateOnly.Parse(de), DateOnly.Parse(ate), eixo, UnidadeExecutanteId: unidadeId);

    /// <summary>
    /// Agendamento das 22h de Brasília conta no dia de Brasília, não no dia seguinte em UTC.
    ///
    /// <para>Sem isto, toda consulta feita à noite devolveria número diferente da mesma consulta
    /// feita de manhã, e o agendamento noturno apareceria no dia errado da tela — bem no horário em
    /// que a operação confere o dia seguinte.</para>
    /// </summary>
    [Fact]
    public async Task Agendamento_noturno_conta_no_dia_de_Brasilia()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        // 01:00 UTC de 11/03 é 22:00 de Brasília do dia 10/03.
        Semear(db, unidade, new DateTime(2026, 3, 11, 1, 0, 0), new DateOnly(2026, 3, 1));
        await db.SaveChangesAsync();

        var servico = Servico(db);

        var noDiaDeBrasilia = await servico.ResumoAsync(Filtro(unidade, "2026-03-10", "2026-03-10"));
        var noDiaEmUtc = await servico.ResumoAsync(Filtro(unidade, "2026-03-11", "2026-03-11"));

        Assert.Equal(1, noDiaDeBrasilia.Regulados);
        Assert.Equal(0, noDiaEmUtc.Regulados);
    }

    /// <summary>
    /// Espera negativa fica fora da mediana, mas é contada e exibida.
    ///
    /// <para>Descartar em silêncio esconderia um erro de cadastro que só se corrige quem sabe que
    /// existe; incluir no cálculo faria o indicador melhorar por causa do defeito. As duas saídas
    /// fáceis estão erradas — daí o par de asserções.</para>
    /// </summary>
    [Fact]
    public async Task Espera_negativa_nao_entra_na_mediana_mas_e_contada()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        // Duas esperas legítimas de 30 dias e uma impossível de -20.
        Semear(db, unidade, new DateTime(2026, 4, 10, 12, 0, 0), new DateOnly(2026, 3, 11));
        Semear(db, unidade, new DateTime(2026, 4, 10, 12, 0, 0), new DateOnly(2026, 3, 11));
        Semear(db, unidade, new DateTime(2026, 4, 10, 12, 0, 0), new DateOnly(2026, 4, 30));
        await db.SaveChangesAsync();

        var resumo = await Servico(db).ResumoAsync(Filtro(unidade, "2026-04-10", "2026-04-10"));

        Assert.Equal(3, resumo.Regulados);
        Assert.Equal(2, resumo.ComEspera);
        Assert.Equal(30, resumo.EsperaMediana);
        Assert.Equal(1, resumo.Inconsistentes);
    }

    /// <summary>
    /// Um caso extremo não desloca a mediana — que é a razão de ela ser a medida escolhida.
    ///
    /// <para>Com esperas de 10, 20 e 2.000 dias, a média daria 676: um número que nenhum dos três
    /// pacientes viveu e que faria a fila parecer catastrófica em qualquer recorte que contivesse
    /// um único caso perdido.</para>
    /// </summary>
    [Fact]
    public async Task Mediana_resiste_ao_caso_extremo()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var agendada = new DateTime(2026, 6, 15, 12, 0, 0);

        Semear(db, unidade, agendada, DateOnly.FromDateTime(agendada).AddDays(-10));
        Semear(db, unidade, agendada, DateOnly.FromDateTime(agendada).AddDays(-20));
        Semear(db, unidade, agendada, DateOnly.FromDateTime(agendada).AddDays(-2000));
        await db.SaveChangesAsync();

        var resumo = await Servico(db).ResumoAsync(Filtro(unidade, "2026-06-15", "2026-06-15"));

        Assert.Equal(20, resumo.EsperaMediana);
        Assert.Equal(2000, resumo.EsperaMaxima);
    }

    /// <summary>
    /// O histograma devolve as sete faixas sempre, inclusive as vazias.
    ///
    /// <para>Se faixa sem ninguém sumisse do resultado, o gráfico mudaria de forma conforme o
    /// filtro e duas consultas deixariam de ser comparáveis a olho — que é exatamente para o que
    /// serve um histograma de faixas fixas.</para>
    /// </summary>
    [Fact]
    public async Task Histograma_mantem_as_faixas_vazias()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        // Só uma solicitação, na faixa de 31 a 60 dias.
        Semear(db, unidade, new DateTime(2026, 7, 20, 12, 0, 0), new DateOnly(2026, 6, 15));
        await db.SaveChangesAsync();

        var faixas = await Servico(db).FaixasEsperaAsync(Filtro(unidade, "2026-07-20", "2026-07-20"));

        Assert.Equal(7, faixas.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], faixas.Select(f => f.Ordem));
        Assert.Equal(1, faixas.Single(f => f.Ordem == 4).Volume);
        Assert.Equal(1, faixas.Sum(f => f.Volume));
    }

    /// <summary>
    /// Trocar o eixo de data troca o conjunto, não a ordenação.
    ///
    /// <para>É a diferença entre "quem foi atendido nestes dias" e "quem pediu nestes dias". Se os
    /// dois eixos devolvessem o mesmo conjunto, o seletor seria decorativo e a tela estaria
    /// prometendo uma leitura que não entrega.</para>
    /// </summary>
    [Fact]
    public async Task Eixo_de_data_seleciona_conjuntos_diferentes()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        // Pedida em janeiro, atendida em agosto: entra por "solicitada" em janeiro,
        // e por "agendada" em agosto — nunca nos dois ao mesmo tempo.
        Semear(db, unidade, new DateTime(2026, 8, 12, 12, 0, 0), new DateOnly(2026, 1, 20));
        await db.SaveChangesAsync();

        var servico = Servico(db);

        var janeiroPorSolicitacao = await servico.ResumoAsync(
            Filtro(unidade, "2026-01-01", "2026-01-31", "solicitada"));
        var janeiroPorAgendamento = await servico.ResumoAsync(
            Filtro(unidade, "2026-01-01", "2026-01-31"));
        var agostoPorAgendamento = await servico.ResumoAsync(
            Filtro(unidade, "2026-08-01", "2026-08-31"));

        Assert.Equal(1, janeiroPorSolicitacao.Regulados);
        Assert.Equal(0, janeiroPorAgendamento.Regulados);
        Assert.Equal(1, agostoPorAgendamento.Regulados);
    }

    /// <summary>
    /// Origem e série executam contra Postgres de verdade.
    ///
    /// <para>Fumaça deliberada, pelo mesmo motivo do teste equivalente em
    /// <c>AgendaAnaliseTests</c>: o que quebra nestes endpoints não é a conta, é a ligação —
    /// parâmetro que o Npgsql recusa, coluna cujo apelido não casa com a propriedade do DTO. O
    /// compilador não vê nada disso, e o sintoma em produção é a seção inteira em 500.</para>
    /// </summary>
    [Fact]
    public async Task Origem_e_serie_executam_contra_o_banco()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);

        Semear(db, unidade, new DateTime(2026, 2, 10, 12, 0, 0), new DateOnly(2026, 1, 10));
        Semear(db, unidade, new DateTime(2026, 3, 10, 12, 0, 0), new DateOnly(2026, 1, 10));
        await db.SaveChangesAsync();

        var servico = Servico(db);
        var filtro = Filtro(unidade, "2026-02-01", "2026-03-31");

        var porSolicitante = await servico.OrigemAsync(filtro, "solicitante", 10);
        var porExecutante = await servico.OrigemAsync(filtro, "executante", 10);
        var serie = await servico.SerieAsync(filtro);

        // Sem unidade solicitante nas linhas semeadas, a origem cai no agrupamento único do nulo.
        Assert.Single(porSolicitante);
        Assert.Equal(2, porSolicitante[0].Volume);

        Assert.Single(porExecutante);
        Assert.Equal(2, porExecutante[0].Volume);

        // Dois meses distintos de agendamento viram duas linhas na série.
        Assert.Equal(2, serie.Count);
        Assert.All(serie, s => Assert.Equal(1, s.Volume));
    }

    /// <summary>
    /// O ranking por espera não é o ranking por volume — e é por isso que a tela oferece os dois.
    ///
    /// <para>Medido em produção em 05/09/2026: Mamografia bilateral tinha 2.301 pedidos com 56 dias
    /// de espera, enquanto Ultrassonografia de mamas tinha 655 com 260. Uma tela que só ordenasse
    /// por volume nunca mostraria o gargalo.</para>
    /// </summary>
    [Fact]
    public async Task Ordenar_por_espera_encontra_o_gargalo_que_o_volume_esconde()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var agendada = new DateTime(2026, 5, 20, 12, 0, 0);
        var dia = DateOnly.FromDateTime(agendada);

        // Muito volume, pouca espera.
        for (var i = 0; i < 5; i++) Semear(db, unidade, agendada, dia.AddDays(-10), "POPULAR");
        // Pouco volume, muita espera.
        Semear(db, unidade, agendada, dia.AddDays(-300), "GARGALO");
        await db.SaveChangesAsync();

        var servico = Servico(db);
        var filtro = Filtro(unidade, "2026-05-20", "2026-05-20");

        var porVolume = await servico.ProcedimentosAsync(filtro, "volume", 10);
        var porEspera = await servico.ProcedimentosAsync(filtro, "espera", 10);

        Assert.Equal("POPULAR", porVolume[0].Procedimento);
        Assert.Equal("GARGALO", porEspera[0].Procedimento);
        Assert.Equal(300, porEspera[0].EsperaMediana);
    }
}
