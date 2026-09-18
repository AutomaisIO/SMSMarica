using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.AgendaRegulacao;
using SMSMais.Core.EstrategiasFila;
using SMSMais.Data;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// O cenário é SQL cru sobre quatro tabelas. O que estes testes prendem não é a aritmética — é a
/// ligação: parâmetro de array que o Npgsql recusa, coluna que não casa com o record, família que
/// deixa o item de fora. Tudo contra Postgres de verdade.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CenarioFilaServiceTests(PostgresFixture fixture)
{
    private static CenarioFilaService Servico(SmsMaisDbContext db) =>
        new(db, new AgendaDemandaService(db, new UsuarioAtualAccessorFake()), new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Lista_traz_o_grupo_com_a_fila_da_familia_inteira()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);

        var lista = await Servico(db).ListarProcedimentosAsync(busca: null, ordenar: "fila");

        var linha = lista.Should().ContainSingle(p => p.Codigo == seed.CodigoGrupo).Subject;
        linha.Nome.Should().Be(seed.NomeGrupo);
        linha.EhGrupo.Should().BeTrue();
        // 3 pediram o grupo + 2 pediram o item: a vaga do grupo serve os dois.
        linha.NaFila.Should().Be(SeedEstrategiasFila.NaFila);
        linha.VagasRegulacaoSemana.Should().Be(2 * SeedEstrategiasFila.VagasPorBloco);
        linha.Unidades.Should().Be(1);
        linha.Profissionais.Should().Be(1);
        linha.EsperaMedianaDias.Should().BeGreaterThan(0);
        linha.TemEstrategia.Should().BeFalse();

        // O item não tem escala: não vira linha própria, e o nome dele não vira "fila sem oferta".
        lista.Should().NotContain(p => p.Nome == seed.NomeItem);

        // Busca por trecho do nome.
        var busca = await Servico(db).ListarProcedimentosAsync(seed.NomeGrupo[^6..], "nome");
        busca.Should().ContainSingle(p => p.Codigo == seed.CodigoGrupo);
    }

    [Fact]
    public async Task Procedimento_so_da_fila_aparece_sem_codigo()
    {
        await using var db = fixture.CriarDbContext();
        var nome = $"SO FILA {Guid.NewGuid():N}"[..30].ToUpperInvariant();
        db.SisregFilaPendentes.Add(new Data.Entities.Sisreg.SisregFilaPendente
        {
            Id = Guid.NewGuid(),
            CodigoSolicitacao = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(),
            DataSolicitacao = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
            ProcedimentoNome = nome,
            PrimeiroVistoEm = DateTime.UtcNow,
            UltimoVistoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var lista = await Servico(db).ListarProcedimentosAsync(nome, null);

        var linha = lista.Should().ContainSingle(p => p.Nome == nome).Subject;
        linha.Codigo.Should().BeNull();
        linha.NaFila.Should().Be(1);
        linha.VagasRegulacaoSemana.Should().Be(0);
    }

    [Fact]
    public async Task Cenario_monta_fila_ritmos_oferta_e_aproveitamento()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);

        var c = await Servico(db).MontarAsync(seed.CodigoGrupo, seed.NomeGrupo);

        // Procedimento e família.
        c.Procedimento.Codigo.Should().Be(seed.CodigoGrupo);
        c.Procedimento.EhGrupo.Should().BeTrue();
        c.Procedimento.Familia.Should().Contain([seed.NomeGrupo, seed.NomeItem]);

        // Fila: quem ainda espera, pelos dois nomes.
        c.Fila.Total.Should().Be(SeedEstrategiasFila.NaFila);
        c.Fila.EsperaMedianaDias.Should().BeGreaterThan(0);
        c.Fila.PorRisco.Values.Sum().Should().Be(SeedEstrategiasFila.NaFila);
        c.Fila.Faixas.Sum(f => f.Volume).Should().Be(SeedEstrategiasFila.NaFila);
        c.Fila.MaisAntigoEm.Should().NotBeNull();

        // Entrada: todo mundo que pediu (fila aberta + quem saiu + marcações), por número.
        c.Entrada.Serie.Should().HaveCount(26);
        c.Entrada.Serie.Sum(s => s.Quantidade).Should().Be(SeedEstrategiasFila.NaFila + 1 + SeedEstrategiasFila.Marcacoes);
        c.Entrada.MediaSemanal12.Should().BeGreaterThan(0);

        // Vazão: as marcações, pela data agendada.
        c.Vazao.Serie.Sum(s => s.Quantidade).Should().Be(SeedEstrategiasFila.Marcacoes);

        // Quem saiu: 1, sem agendar.
        c.SaidaSemAgendarFracao.Should().Be(1);

        // Oferta: 2 blocos/semana de 10 vagas de regulação, um profissional, seg e qua, 4 h.
        c.Oferta.VagasRegulacaoSemana.Should().Be(2 * SeedEstrategiasFila.VagasPorBloco);
        c.Oferta.VagasPrimeiraVezSemana.Should().Be(10);
        c.Oferta.VagasReservaSemana.Should().Be(10);
        c.Oferta.Profissionais.Should().ContainSingle().Which.Dias.Should().Equal(1, 3);
        c.Oferta.Unidades.Should().ContainSingle().Which.UnidadeId.Should().Be(seed.UnidadeId);
        c.Oferta.DiasSemana.Should().Equal(1, 3);
        // 8 turnos (seg+qua × 4 semanas) → 2 por semana, de um profissional; 20 vagas ÷ 2 turnos = 10 por
        // turno — a identidade que faz "sem mudança" reproduzir a oferta de hoje.
        c.Oferta.TurnosSemana.Should().Be(2);
        c.Oferta.TurnosPorProfissionalSemana.Should().Be(2);
        c.Oferta.AtendimentosPorTurno.Should().Be(10);
        c.Oferta.HorasDeclaradasSemana.Should().Be(8);
        c.Oferta.HoraInicioTipica.Should().Be(new TimeOnly(8, 0));
        c.Oferta.HoraFimTipica.Should().Be(new TimeOnly(12, 0));

        // Ocupação nas 8 semanas passadas: 16 blocos × 10 vagas = 160 ofertadas, 4 marcações.
        c.Ocupacao.VagasRegulacaoOfertadas.Should().Be(160);
        c.Ocupacao.Agendados.Should().Be(SeedEstrategiasFila.Marcacoes);
        c.Ocupacao.Aproveitamento.Should().BeApproximately(0.025, 0.0001);

        // Parâmetros iniciais reproduzem a oferta: capacidade = vagas × aproveitamento.
        var p = c.ParametrosIniciais;
        p.Profissionais.Valor.Should().Be(1);
        p.VagasSemanais().Should().BeApproximately(20, 0.01);
        p.EntradaSemanal.Travado.Should().BeTrue();
        p.Aproveitamento.Valor.Should().BeApproximately(0.03, 0.01);

        var proj = SimuladorFila.Projetar(p, c.Fila.Total);
        proj.VagasSemanais.Should().BeApproximately(20, 0.01);
    }

    [Fact]
    public async Task Cenario_pelo_item_inclui_a_escala_do_grupo()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);

        var c = await Servico(db).MontarAsync(seed.CodigoItem, seed.NomeItem);

        c.Procedimento.EhGrupo.Should().BeFalse();
        c.Procedimento.Familia.Should().Contain(seed.NomeGrupo);
        // A vaga de grupo atende quem pediu o item: oferta e fila são as mesmas.
        c.Oferta.VagasRegulacaoSemana.Should().Be(2 * SeedEstrategiasFila.VagasPorBloco);
        c.Fila.Total.Should().Be(SeedEstrategiasFila.NaFila);
    }

    [Fact]
    public async Task Cenario_de_procedimento_sem_escala_nao_explode_e_tem_parametros_de_partida()
    {
        await using var db = fixture.CriarDbContext();

        var c = await Servico(db).MontarAsync(null, $"NADA {Guid.NewGuid():N}");

        c.Fila.Total.Should().Be(0);
        c.Oferta.Profissionais.Should().BeEmpty();
        c.Ocupacao.Aproveitamento.Should().BeNull();
        c.ParametrosIniciais.Profissionais.Valor.Should().Be(0);
        c.ParametrosIniciais.TurnosPorProfissionalSemana.Valor.Should().Be(2);
        c.ParametrosIniciais.AtendimentosPorTurno.Valor.Should().Be(10);
        c.ParametrosIniciais.Aproveitamento.Valor.Should().Be(0.85);
    }
}
