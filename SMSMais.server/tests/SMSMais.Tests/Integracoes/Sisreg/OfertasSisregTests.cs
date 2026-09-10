using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Tela de Ofertas — o que abriu no SISREG.
///
/// <para><b>Por que este teste existe contra banco de verdade.</b> A primeira versão do serviço
/// agrupava no SQL e projetava <c>Distinct()</c> sobre os dias da semana dentro do <c>GroupBy</c>.
/// Compilava, passava em teste de unidade, e estourava <b>em produção</b> com
/// <i>"Unable to translate a collection subquery in a projection"</i> — 500 na cara do operador,
/// minutos depois do deploy (09/09/2026). Tradução de query só se prova contra um banco; por isso
/// o caminho inteiro roda aqui.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class OfertasSisregTests(PostgresFixture fixture)
{
    private static async Task<Guid> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE OFERTA {Random.Shared.Next(100_000, 999_999)}",
            Cnes = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private static SisregEscala Escala(
        Guid unidadeId, string procedimento, DayOfWeek dia, int vagas, DateOnly fim, DateTime criadoEm) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodigoEscala = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            UnidadeId = unidadeId,
            UnidadeNomeSisreg = "X",
            Cnes = "1234567",
            ProcedimentoCodigo = procedimento,
            ProcedimentoNome = $"PROC {procedimento}",
            CboDescricao = "MEDICO",
            DiaSemana = dia,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(12, 0),
            VigenciaInicio = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            VigenciaFim = fim,
            VagasTotal = vagas,
            Status = StatusEscalaSisreg.Ativa,
            Ausente = false,
            CriadoEm = criadoEm,
        };

    /// <summary>
    /// O caso que quebrou em produção: várias escalas do mesmo procedimento, em dias diferentes,
    /// têm de virar UMA oferta com a soma das vagas e a lista de dias — e a consulta tem de
    /// realmente executar no Postgres.
    /// </summary>
    [Fact]
    public async Task Agenda_nova_agrupa_os_blocos_da_semana_numa_oferta_so()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var proc = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var fim = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var agora = DateTime.UtcNow;

        db.SisregEscalas.AddRange(
            Escala(unidadeId, proc, DayOfWeek.Monday, 10, fim, agora),
            Escala(unidadeId, proc, DayOfWeek.Wednesday, 12, fim, agora),
            Escala(unidadeId, proc, DayOfWeek.Friday, 10, fim, agora));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        var oferta = Assert.Single(r.AgendasNovas, a => a.ProcedimentoCodigo == proc);
        Assert.Equal(3, oferta.Blocos);
        Assert.Equal(32, oferta.Vagas);
        Assert.Equal([1, 3, 5], oferta.DiasSemana);
    }

    /// <summary>
    /// Bloco que já venceu não é oferta, é histórico — mostrar seria mandar o operador atrás de
    /// vaga que não existe mais.
    /// </summary>
    [Fact]
    public async Task Escala_vencida_nao_e_oferta()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var proc = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        db.SisregEscalas.Add(Escala(unidadeId, proc, DayOfWeek.Monday, 10, ontem, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        Assert.DoesNotContain(r.AgendasNovas, a => a.ProcedimentoCodigo == proc);
    }

    /// <summary>
    /// A janela é o que define "novo": uma escala vista há muito tempo não volta a ser novidade
    /// porque alguém abriu a tela.
    /// </summary>
    [Fact]
    public async Task Escala_vista_fora_da_janela_nao_aparece()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var proc = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var fim = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        db.SisregEscalas.Add(
            Escala(unidadeId, proc, DayOfWeek.Monday, 10, fim, DateTime.UtcNow.AddDays(-40)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        Assert.DoesNotContain(r.AgendasNovas, a => a.ProcedimentoCodigo == proc);
    }

    /// <summary>
    /// A janela pedida é limitada a 1–90 dias: um pedido absurdo não pode virar varredura da base
    /// inteira nem janela negativa.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(9999, 90)]
    [InlineData(7, 7)]
    public async Task Janela_pedida_e_limitada(int pedido, int esperado)
    {
        await using var db = fixture.CriarDbContext();

        var r = await new OfertasSisregService(db).ListarAsync(pedido);

        Assert.Equal(esperado, r.JanelaDias);
    }

    /// <summary>
    /// <b>Carga não é novidade.</b> A primeira sincronização de todas criou 17.452 escalas de uma
    /// vez; sem esta regra a tela anunciava <b>10.456 vagas novas</b> na janela de 7 dias contra
    /// 101 reais (medido em produção em 09/09/2026, minutos depois do deploy). O operador
    /// aprenderia no primeiro dia que o número é mentira — e uma tela em que não se acredita não
    /// serve para nada.
    /// </summary>
    [Fact]
    public async Task Escala_que_nasceu_numa_CARGA_nao_conta_como_agenda_nova()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var daCarga = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var organica = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var fim = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        var inicioCarga = DateTime.UtcNow.AddHours(-2);
        db.SisregEscalaSincronizacaoExecucoes.Add(new SisregEscalaSincronizacaoExecucao
        {
            Id = Guid.CreateVersion7(),
            Disparo = DisparoSincronizacao.Manual,
            Status = StatusVarredura.Concluida,
            EscalasNovas = 17_452,
            IniciadoEm = inicioCarga,
            FinalizadoEm = inicioCarga.AddMinutes(1),
        });

        db.SisregEscalas.AddRange(
            // Nasceu no meio da carga: é retrato inicial, não oferta.
            Escala(unidadeId, daCarga, DayOfWeek.Monday, 500, fim, inicioCarga.AddSeconds(30)),
            // Nasceu depois, num sincronismo comum: é oferta de verdade.
            Escala(unidadeId, organica, DayOfWeek.Tuesday, 7, fim, DateTime.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        Assert.DoesNotContain(r.AgendasNovas, a => a.ProcedimentoCodigo == daCarga);
        Assert.Contains(r.AgendasNovas, a => a.ProcedimentoCodigo == organica);
    }

    private static SisregFilaPendente NaFila(
        string procedimento, DateOnly pedido, int? risco = null, string? nome = null, int? idade = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodigoSolicitacao = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            DataSolicitacao = pedido,
            Risco = risco,
            PacienteNome = nome ?? "PACIENTE TESTE",
            IdadeAnos = idade,
            ProcedimentoNome = procedimento,
            PrimeiroVistoEm = DateTime.UtcNow,
            UltimoVistoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };

    /// <summary>
    /// A ordem padrão é a mais defensável numa fila pública: quem chegou primeiro é chamado
    /// primeiro. Qualquer outra precisa de alguém escolhendo, e a escolha fica registrada na URL.
    /// </summary>
    [Fact]
    public async Task Fila_vem_por_ordem_de_chegada_por_padrao()
    {
        await using var db = fixture.CriarDbContext();
        var proc = $"CONSULTA TESTE {Random.Shared.Next(100_000, 999_999)}";
        db.SisregFilaPendentes.AddRange(
            NaFila(proc, new DateOnly(2026, 3, 10), nome: "MAIS RECENTE"),
            NaFila(proc, new DateOnly(2025, 1, 5), nome: "MAIS ANTIGO"),
            NaFila(proc, new DateOnly(2026, 1, 20), nome: "DO MEIO"));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).FilaDaOfertaAsync(proc, null, 100, 0);

        Assert.Equal(3, r.Total);
        Assert.Equal(["MAIS ANTIGO", "DO MEIO", "MAIS RECENTE"], r.Pessoas.Select(p => p.Nome));
        Assert.True(r.Pessoas[0].EsperandoHaDias > r.Pessoas[2].EsperandoHaDias);
    }

    /// <summary>
    /// Ordenar por risco põe o vermelho na frente — e "não classificado" NÃO pode passar na frente
    /// de um vermelho só por ser nulo.
    /// </summary>
    [Fact]
    public async Task Por_risco_o_vermelho_vem_primeiro_e_o_sem_classificacao_por_ultimo()
    {
        await using var db = fixture.CriarDbContext();
        var proc = $"CONSULTA TESTE {Random.Shared.Next(100_000, 999_999)}";
        db.SisregFilaPendentes.AddRange(
            NaFila(proc, new DateOnly(2026, 1, 1), risco: null, nome: "SEM RISCO"),
            NaFila(proc, new DateOnly(2026, 1, 1), risco: 3, nome: "AZUL"),
            NaFila(proc, new DateOnly(2026, 1, 1), risco: 0, nome: "VERMELHO"));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).FilaDaOfertaAsync(proc, "risco", 100, 0);

        Assert.Equal(["VERMELHO", "AZUL", "SEM RISCO"], r.Pessoas.Select(p => p.Nome));
    }

    /// <summary>
    /// Comparação exata, sem <c>Contains</c>: "CONSULTA EM CARDIOLOGIA" não pode arrastar
    /// "CONSULTA EM CARDIOLOGIA - PEDIATRIA", que é outra fila com outra espera.
    /// </summary>
    [Fact]
    public async Task Procedimento_parecido_nao_entra_na_fila_de_outro()
    {
        await using var db = fixture.CriarDbContext();
        var n = Random.Shared.Next(100_000, 999_999);
        var proc = $"CONSULTA {n}";
        db.SisregFilaPendentes.AddRange(
            NaFila(proc, new DateOnly(2026, 1, 1)),
            NaFila($"{proc} - PEDIATRIA", new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).FilaDaOfertaAsync(proc, null, 100, 0);

        Assert.Equal(1, r.Total);
    }

    /// <summary>Quem já saiu não é fila — continuar oferecendo seria chamar quem já foi atendido.</summary>
    [Fact]
    public async Task Quem_saiu_da_fila_nao_aparece()
    {
        await using var db = fixture.CriarDbContext();
        var proc = $"CONSULTA TESTE {Random.Shared.Next(100_000, 999_999)}";
        var saiu = NaFila(proc, new DateOnly(2026, 1, 1));
        saiu.SaiuEm = DateTime.UtcNow;
        saiu.SaiuPara = SaidaDaFilaSisreg.Agendada;
        db.SisregFilaPendentes.AddRange(saiu, NaFila(proc, new DateOnly(2026, 2, 1)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).FilaDaOfertaAsync(proc, null, 100, 0);

        Assert.Equal(1, r.Total);
    }

    [Fact]
    public async Task Procedimento_vazio_e_recusado()
    {
        await using var db = fixture.CriarDbContext();

        await Assert.ThrowsAsync<SMSMais.Core.Common.Excecoes.ValidacaoException>(() =>
            new OfertasSisregService(db).FilaDaOfertaAsync("  ", null, 100, 0));
    }

    // ------------------------------------------------------------------ datas da oferta

    /// <summary>Próxima ocorrência do dia da semana a partir de amanhã (dia de Brasília).</summary>
    private static DateOnly Proxima(DayOfWeek dia)
    {
        var d = DateOnly.FromDateTime(SMSMais.Core.Common.Tempo.FusoBrasilia.ParaExibicao(DateTime.UtcNow)).AddDays(1);
        while (d.DayOfWeek != dia) d = d.AddDays(1);
        return d;
    }

    private static string CodigoItem() => Random.Shared.Next(1000, 9999) + Random.Shared.Next(100, 999).ToString();

    private static SisregEscala EscalaDeVagas(
        Guid unidadeId, string codigo, DayOfWeek dia, int primeiraVez, int total,
        DateOnly? inicio = null, bool agendaLocal = false) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodigoEscala = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            UnidadeId = unidadeId,
            UnidadeNomeSisreg = "X",
            Cnes = "1234567",
            ProfissionalNome = "DRA TESTE",
            ProcedimentoCodigo = codigo,
            ProcedimentoNome = codigo.EndsWith("000") ? $"GRUPO - TESTE {codigo}" : $"PROC {codigo}",
            EhGrupo = codigo.EndsWith("000"),
            DiaSemana = dia,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(12, 0),
            VigenciaInicio = inicio ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
            VigenciaFim = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
            VagasPrimeiraVez = primeiraVez,
            VagasTotal = total,
            AgendaLocal = agendaLocal,
            Status = StatusEscalaSisreg.Ativa,
            Ausente = false,
            CriadoEm = DateTime.UtcNow.AddDays(-40),
        };

    private static Solicitacao Agendado(Guid unidadeId, string codigo, DateOnly dia) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CodigoSolicitacao = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            UnidadeExecutanteId = unidadeId,
            ProcedimentoCodigoSisreg = codigo,
            // 12:00 UTC = 09:00 em Brasília: o mesmo dia dos dois lados.
            DataAgendada = dia.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            Status = StatusSolicitacao.Agendada,
            CriadoEm = DateTime.UtcNow,
        };

    /// <summary>
    /// A queixa que originou isto (10/09/2026): a tela mostrava a VIGÊNCIA como se fosse o período
    /// com vaga. Um dia lotado não é vaga — a primeira data livre é a do dia seguinte com sobra, e
    /// só vaga de PRIMEIRA VEZ conta, porque é a que a regulação marca.
    /// </summary>
    [Fact]
    public async Task Primeira_vaga_livre_pula_o_dia_lotado_e_conta_so_primeira_vez()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var codigo = CodigoItem();
        var segunda = Proxima(DayOfWeek.Monday);

        // 10 vagas no dia, só 6 de primeira vez.
        db.SisregEscalas.Add(EscalaDeVagas(unidadeId, codigo, DayOfWeek.Monday, primeiraVez: 6, total: 10));
        for (var i = 0; i < 10; i++) db.Solicitacoes.Add(Agendado(unidadeId, codigo, segunda));
        for (var i = 0; i < 7; i++) db.Solicitacoes.Add(Agendado(unidadeId, codigo, segunda.AddDays(7)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).DatasDaOfertaAsync(codigo, 60);

        var u = Assert.Single(r.Unidades, x => x.UnidadeId == unidadeId);
        var dia1 = Assert.Single(u.Dias, d => d.Data == segunda);
        var dia2 = Assert.Single(u.Dias, d => d.Data == segunda.AddDays(7));
        var dia3 = Assert.Single(u.Dias, d => d.Data == segunda.AddDays(14));

        Assert.Equal(0, dia1.Livres);            // lotado
        Assert.Equal(3, dia2.Livres);            // 10 − 7 = 3, cabe nas 6 de primeira vez
        Assert.Equal(6, dia3.Livres);            // vazio: o teto é a primeira vez, não o total
        Assert.Equal(6, dia3.Vagas);
        Assert.Equal(segunda.AddDays(7), u.PrimeiraVagaLivre);
    }

    /// <summary>
    /// O caso do ECG do CDT: escala ativa há meses, 280 vagas por semana declaradas e nenhum
    /// agendamento futuro — o SISREG não está ofertando. A tela avisa em vez de anunciar vaga.
    /// Agenda aberta ontem não é suspeita: ainda não teve tempo de receber marcação.
    /// </summary>
    [Fact]
    public async Task Agenda_antiga_sem_nenhum_agendamento_futuro_e_marcada_suspeita()
    {
        await using var db = fixture.CriarDbContext();
        var antiga = await CriarUnidadeAsync(db);
        var nova = await CriarUnidadeAsync(db);
        var codigo = CodigoItem();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        db.SisregEscalas.AddRange(
            EscalaDeVagas(antiga, codigo, DayOfWeek.Tuesday, 20, 20, inicio: hoje.AddDays(-200)),
            EscalaDeVagas(nova, codigo, DayOfWeek.Tuesday, 20, 20, inicio: hoje.AddDays(-1)));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).DatasDaOfertaAsync(codigo, 60);

        Assert.True(Assert.Single(r.Unidades, u => u.UnidadeId == antiga).SemAgendamentoFuturo);
        Assert.False(Assert.Single(r.Unidades, u => u.UnidadeId == nova).SemAgendamentoFuturo);
    }

    /// <summary>
    /// A vaga de ITEM é marcada na escala do GRUPO ("GRUPO - ULTRASONOGRAFIA" recebe a
    /// transvaginal), e em escala de grupo qualquer item do grupo ocupa a vaga.
    /// </summary>
    [Fact]
    public async Task Item_casa_com_a_escala_do_grupo_e_qualquer_item_ocupa_a_vaga_do_grupo()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var prefixo = Random.Shared.Next(1000, 9999).ToString();
        var grupo = prefixo + "000";
        var item = prefixo + "012";
        var outroItem = prefixo + "034";
        var quarta = Proxima(DayOfWeek.Wednesday);

        db.SisregEscalas.Add(EscalaDeVagas(unidadeId, grupo, DayOfWeek.Wednesday, 5, 5));
        db.Solicitacoes.AddRange(Agendado(unidadeId, item, quarta), Agendado(unidadeId, outroItem, quarta));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).DatasDaOfertaAsync(item, 30);

        var u = Assert.Single(r.Unidades, x => x.UnidadeId == unidadeId);
        var dia = Assert.Single(u.Dias, d => d.Data == quarta);
        Assert.Equal(2, dia.Agendados);
        Assert.Equal(3, dia.Livres);
    }

    /// <summary>
    /// Agenda local não é oferta para a regulação: a unidade marca direto e o regulador nunca vê.
    /// A tela separa, e a regulada vem primeiro.
    /// </summary>
    [Fact]
    public async Task Agenda_local_vem_marcada_e_depois_da_regulada()
    {
        await using var db = fixture.CriarDbContext();
        var local = await CriarUnidadeAsync(db);
        var regulada = await CriarUnidadeAsync(db);
        var codigo = CodigoItem();

        db.SisregEscalas.AddRange(
            EscalaDeVagas(local, codigo, DayOfWeek.Thursday, 5, 5, agendaLocal: true),
            EscalaDeVagas(regulada, codigo, DayOfWeek.Thursday, 5, 5));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).DatasDaOfertaAsync(codigo, 30);
        var nossas = r.Unidades.Where(u => u.UnidadeId == local || u.UnidadeId == regulada).ToList();

        Assert.Equal([regulada, local], nossas.Select(u => u.UnidadeId));
        Assert.True(nossas[1].AgendaLocal);
        Assert.False(nossas[0].AgendaLocal);
    }

    /// <summary>
    /// O caso do ECG do Bairro da Amizade (10/09/2026): a MESMA unidade tem agenda local num período
    /// e regulada em outro. O regulador só enxerga a regulada — a primeira vaga dele é a do bloco
    /// regulado, não a do local que vem antes. A unidade aparece duas vezes, uma de cada tipo.
    /// </summary>
    [Fact]
    public async Task Unidade_mista_separa_as_datas_locais_das_reguladas()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var codigo = CodigoItem();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var sexta = Proxima(DayOfWeek.Friday);

        var local = EscalaDeVagas(unidadeId, codigo, DayOfWeek.Friday, 10, 10, agendaLocal: true);
        local.VigenciaFim = sexta.AddDays(20);                      // as 3 primeiras sextas: local
        var regulada = EscalaDeVagas(unidadeId, codigo, DayOfWeek.Friday, 10, 10, inicio: sexta.AddDays(21));
        db.SisregEscalas.AddRange(local, regulada);
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).DatasDaOfertaAsync(codigo, 60);
        var daUnidade = r.Unidades.Where(u => u.UnidadeId == unidadeId).ToList();

        Assert.Equal(2, daUnidade.Count);
        var reg = Assert.Single(daUnidade, u => !u.AgendaLocal);
        var loc = Assert.Single(daUnidade, u => u.AgendaLocal);
        Assert.Equal(sexta.AddDays(21), reg.PrimeiraVagaLivre);
        Assert.Equal(sexta, loc.PrimeiraVagaLivre);
        Assert.All(reg.Dias, d => Assert.True(d.Data >= sexta.AddDays(21)));
        Assert.True(hoje < reg.PrimeiraVagaLivre);
    }

    [Fact]
    public async Task Agenda_nova_diz_se_e_agenda_local()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var proc = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var fim = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var escala = Escala(unidadeId, proc, DayOfWeek.Monday, 10, fim, DateTime.UtcNow);
        escala.AgendaLocal = true;
        db.SisregEscalas.Add(escala);
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        Assert.True(Assert.Single(r.AgendasNovas, a => a.ProcedimentoCodigo == proc).AgendaLocal);
    }

    /// <summary>
    /// A vaga de item serve a quem pediu o GRUPO inteiro: a fila guarda o que foi pedido, e em
    /// ultrassom isso costuma ser "GRUPO - ULTRASONOGRAFIA". Casar só o nome do item daria zero.
    /// </summary>
    [Fact]
    public async Task Fila_de_item_inclui_quem_pediu_o_grupo()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = await CriarUnidadeAsync(db);
        var prefixo = Random.Shared.Next(1000, 9999).ToString();
        var grupo = prefixo + "000";
        var item = prefixo + "056";
        var escalaGrupo = EscalaDeVagas(unidadeId, grupo, DayOfWeek.Friday, 5, 5);
        db.SisregEscalas.Add(escalaGrupo);
        db.SisregFilaPendentes.AddRange(
            NaFila($"PROC {item}", new DateOnly(2026, 1, 1), nome: "PEDIU O ITEM"),
            NaFila(escalaGrupo.ProcedimentoNome, new DateOnly(2025, 6, 1), nome: "PEDIU O GRUPO"));
        await db.SaveChangesAsync();

        var r = await new OfertasSisregService(db).FilaDaOfertaAsync($"PROC {item}", null, 100, 0, item);

        Assert.Equal(["PEDIU O GRUPO", "PEDIU O ITEM"], r.Pessoas.Select(p => p.Nome));
        Assert.Contains(escalaGrupo.ProcedimentoNome, r.ProcedimentosIncluidos);
    }

    /// <summary>
    /// A consulta de vagas liberadas (join com solicitação + subconsulta de espera em SQL cru)
    /// também precisa executar de verdade — é o outro caminho que só um banco prova.
    /// </summary>
    [Fact]
    public async Task Consulta_de_vagas_liberadas_executa_no_banco()
    {
        await using var db = fixture.CriarDbContext();

        var r = await new OfertasSisregService(db).ListarAsync(7);

        Assert.NotNull(r.VagasLiberadas);
        Assert.All(r.VagasLiberadas, v => Assert.True(v.DataAgendada >= DateTime.UtcNow.AddDays(-2)));
    }
}
