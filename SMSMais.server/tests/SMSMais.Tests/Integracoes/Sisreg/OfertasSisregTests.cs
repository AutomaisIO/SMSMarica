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
