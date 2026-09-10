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
