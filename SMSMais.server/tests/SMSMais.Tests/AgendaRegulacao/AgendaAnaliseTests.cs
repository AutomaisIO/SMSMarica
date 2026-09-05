using SMSMais.Core.AgendaRegulacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.AgendaRegulacao;

/// <summary>
/// Cruzamento de <b>oferta</b> (escala publicada pelo SISREG) com <b>ocupação</b> (agendamentos já
/// importados).
///
/// <para><b>Por que estes testes existem, concretamente.</b> Este serviço é SQL cru montado por
/// concatenação, e nasceu sem cobertura nenhuma. O primeiro teste escrito contra ele encontrou um
/// defeito que derrubava <em>todos</em> os endpoints da tela em tempo de execução: as datas do
/// filtro iam como <c>DateTime</c> com <c>Kind=Unspecified</c>, e o Npgsql infere
/// <c>timestamptz</c> para <c>DateTime</c> — a consulta morria com "Cannot write DateTime with
/// Kind=Unspecified" antes mesmo de chegar ao <c>::date</c> do SQL. Compilava, o SQL estava certo,
/// e a tela dava 500 em toda consulta. Daí o teste de fumaça abaixo cobrir <b>cada</b> endpoint:
/// o que quebra aqui não é a lógica, é a ligação com o banco.</para>
///
/// <para>O segundo teste prende a regra que a tela existe para calcular: a escala é um bloco
/// semanal recorrente, e "quantas sextas cabem na vigência" é a conta de onde sai toda a oferta.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgendaAnaliseTests(PostgresFixture fixture)
{
    private static AgendaAnaliseService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake());

    private static async Task<Guid> UnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE ANALISE {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    /// <summary>Uma escala semanal ativa. Código só de dígitos e único — o índice natural é único.</summary>
    private static SisregEscala Escala(
        Guid unidadeId, string cpf, DayOfWeek dia, DateOnly de, DateOnly ate, int vagas)
    {
        return new SisregEscala
        {
            Id = Guid.NewGuid(),
            CodigoEscala = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            UnidadeId = unidadeId,
            Cnes = "9999999",
            UnidadeNomeSisreg = "UNIDADE DE TESTE",
            ProfissionalCpf = cpf,
            ProfissionalNome = "PROFISSIONAL DE TESTE",
            CboCodigo = "225320",
            CboDescricao = "MEDICO",
            ProcedimentoCodigo = "0229000",
            ProcedimentoNome = "GRUPO - ULTRASSONOGRAFIA",
            EhGrupo = true,
            DiaSemana = dia,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(12, 0),
            VigenciaInicio = de,
            VigenciaFim = ate,
            VagasPrimeiraVez = vagas,
            VagasRetorno = 0,
            VagasReserva = 0,
            VagasTotal = vagas,
            Status = StatusEscalaSisreg.Ativa,
            VistoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Todo endpoint da tela executa contra Postgres de verdade.
    ///
    /// <para>É teste de fumaça de propósito: o defeito que ele prende não é de cálculo, é de
    /// ligação — parâmetro que o Npgsql recusa, coluna com nome que não casa com a propriedade do
    /// DTO, cast que o Postgres não aceita. Nada disso aparece no compilador, e o sintoma em
    /// produção é a tela inteira em 500.</para>
    /// </summary>
    [Fact]
    public async Task Todos_os_endpoints_executam_contra_o_banco()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

        db.SisregEscalas.Add(Escala(
            unidade, cpf, DayOfWeek.Friday,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), vagas: 10));
        await db.SaveChangesAsync();

        var servico = Servico(db);
        var filtro = new AgendaFiltro(
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), UnidadeId: unidade);

        var resumo = await servico.ResumoAsync(filtro);
        var dias = await servico.ListarDiasAsync(filtro, 0, 50);
        var ranking = await servico.RankingAsync(filtro, "unidade", 10);
        var semana = await servico.PorDiaSemanaAsync(filtro);
        var serie = await servico.SerieAsync(filtro);
        var detalhe = await servico.DetalharDiaAsync(unidade, cpf, new DateOnly(2026, 2, 6));
        var opcoes = await servico.OpcoesAsync();

        Assert.True(resumo.Vagas > 0);
        Assert.NotEmpty(dias.Itens);
        Assert.NotEmpty(ranking);
        Assert.NotEmpty(semana);
        // A série cobre o intervalo dia a dia, inclusive os dias sem escala nenhuma.
        Assert.Equal(28, serie.Count);
        Assert.Equal(10, detalhe.Vagas);
        Assert.NotNull(opcoes);
    }

    /// <summary>
    /// A oferta é a escala semanal <b>expandida</b> pelas ocorrências do dia da semana dentro da
    /// vigência — não o número de vagas da linha.
    ///
    /// <para>Fevereiro de 2026 tem quatro sextas (6, 13, 20 e 27). Uma escala de 10 vagas às sextas
    /// vale 40 vagas no mês. Ler a coluna de vagas direto daria 10 e subestimaria a capacidade da
    /// rede em quatro vezes — que é o erro que faz alguém concluir que falta médico onde sobra
    /// agenda.</para>
    /// </summary>
    [Fact]
    public async Task Oferta_expande_a_escala_semanal_pelas_ocorrencias_do_dia()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

        db.SisregEscalas.Add(Escala(
            unidade, cpf, DayOfWeek.Friday,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), vagas: 10));
        await db.SaveChangesAsync();

        var resumo = await Servico(db).ResumoAsync(new AgendaFiltro(
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), UnidadeId: unidade));

        Assert.Equal(40, resumo.Vagas);
        Assert.Equal(4, resumo.DiasComOferta);
    }

    /// <summary>
    /// A vigência recorta a expansão: sexta fora da vigência não vira oferta.
    ///
    /// <para>Sem o recorte, uma escala encerrada continuaria produzindo vaga fantasma para sempre —
    /// e vaga fantasma no futuro é exatamente o que faz a tela dizer "há disponibilidade" onde não
    /// há agenda publicada.</para>
    /// </summary>
    [Fact]
    public async Task Vigencia_recorta_a_expansao()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await UnidadeAsync(db);
        var cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

        // Vigência acaba em 14/02: valem as sextas 6 e 13, não as de 20 e 27.
        db.SisregEscalas.Add(Escala(
            unidade, cpf, DayOfWeek.Friday,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 14), vagas: 10));
        await db.SaveChangesAsync();

        var resumo = await Servico(db).ResumoAsync(new AgendaFiltro(
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), UnidadeId: unidade));

        Assert.Equal(20, resumo.Vagas);
        Assert.Equal(2, resumo.DiasComOferta);
    }
}
