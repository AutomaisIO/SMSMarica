using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Catalogo;

/// <summary>
/// Busca de procedimento — a tela que abre o wizard de solicitação.
///
/// <para>Três coisas não podem quebrar em silêncio: quem digita o nome exato tem de ver o
/// procedimento no topo; a queda do provedor de embeddings não pode virar "nada encontrado"
/// (o operador concluiria que o procedimento não existe); e a oferta interna tem de contar só
/// escala que vale hoje — <c>Ativa</c> no SISREG não quer dizer vigente.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoProcedimentoBuscaTests(PostgresFixture fixture)
{
    private static RegulacaoCatalogoService Catalogo(SmsMaisDbContext db, EmbeddingsFake fake) =>
        new(db, fake, new UsuarioAtualAccessorFake(), NullLogger<RegulacaoCatalogoService>.Instance);

    private static RegulacaoProcedimentoBuscaService Busca(
        SmsMaisDbContext db, EmbeddingsFake fake, CacheVetorConsulta? cache = null) =>
        new(db, fake, cache ?? new CacheVetorConsulta(),
            new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), new UsuarioAtualAccessorFake()),
            NullLogger<RegulacaoProcedimentoBuscaService>.Instance);

    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<SerCatalogoRecurso> RecursoSerAsync(SmsMaisDbContext db, string rotulo)
    {
        var r = new SerCatalogoRecurso
        {
            Id = Guid.NewGuid(),
            Tipo = TipoRecursoSer.Consulta,
            Valor = Random.Shared.Next(100_000, 999_999).ToString(),
            Rotulo = rotulo,
            AmbulatorioEstadual = false,
            SincronizadoEm = DateTime.UtcNow,
            CamposLidos = true,
        };
        db.SerCatalogoRecursos.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    [Fact]
    public async Task Termo_curto_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var busca = Busca(db, new EmbeddingsFake());

        var acao = () => busca.BuscarAsync("ab", null, 20, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Lexical_encontra_pelo_rotulo_da_origem_e_pontua_cheio()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var rotulo = $"REUMATOLOGIA {sufixo}";
        await RecursoSerAsync(db, rotulo);

        var fake = new EmbeddingsFake();
        await Catalogo(db, fake).SincronizarAsync(CancellationToken.None);

        var r = await Busca(db, fake).BuscarAsync($"REUMATOLOGIA {sufixo}", null, 20, CancellationToken.None);

        r.Degradada.Should().BeFalse();
        var achado = r.Itens.FirstOrDefault(i => i.Nome == rotulo);
        achado.Should().NotBeNull();
        achado!.Score.Should().Be(1.0, "casou pelo texto — tem de vir com pontuação cheia");
        achado.ExisteExterno.Ser.Should().BeTrue();
        achado.ExisteExterno.Sernit.Should().BeFalse();
    }

    [Fact]
    public async Task Provedor_fora_devolve_degradada_com_lexical()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var rotulo = $"GERIATRIA {sufixo}";
        await RecursoSerAsync(db, rotulo);

        var fakeOk = new EmbeddingsFake();
        await Catalogo(db, fakeOk).SincronizarAsync(CancellationToken.None);

        // Agora o provedor cai: a busca continua, só sem a parte semântica.
        var fakeFora = new EmbeddingsFake { FalharCom = new HttpRequestException("fora do ar") };
        var r = await Busca(db, fakeFora).BuscarAsync($"GERIATRIA {sufixo}", null, 20, CancellationToken.None);

        r.Degradada.Should().BeTrue();
        r.Itens.Should().Contain(i => i.Nome == rotulo,
            "cair o provedor não pode virar 'nada encontrado' — o lexical ainda responde");
    }

    [Fact]
    public async Task Filtro_por_tipo_exclui_o_que_nao_e_do_tipo()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var consulta = $"HEMATOLOGIA {sufixo}";
        await RecursoSerAsync(db, consulta);

        var fake = new EmbeddingsFake();
        await Catalogo(db, fake).SincronizarAsync(CancellationToken.None);

        var comFiltroErrado = await Busca(db, fake)
            .BuscarAsync(consulta, TipoProcedimentoRegulacao.Exame, 20, CancellationToken.None);

        comFiltroErrado.Itens.Should().NotContain(i => i.Nome == consulta);
    }

    [Fact]
    public async Task Executantes_internos_so_escala_ativa_e_vigente()
    {
        await using var db = fixture.CriarDbContext();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var codigo = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var nome = $"CONSULTA TESTE OFERTA {Sufixo()}";

        db.SisregProcedimentosSigtap.Add(new SisregProcedimentoSigtap
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Nome = nome,
            Grupo = false,
            PrimeiroVistoEm = DateTime.UtcNow,
            VistoEm = DateTime.UtcNow,
        });

        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID {Sufixo()}", CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        // Uma vale (ativa, vigente, presente). As outras três são exatamente os jeitos de não valer.
        db.SisregEscalas.AddRange(
            Escala(unidade.Id, codigo, StatusEscalaSisreg.Ativa, hoje.AddDays(30), ausente: false, vagas: 7),
            Escala(unidade.Id, codigo, StatusEscalaSisreg.Inativa, hoje.AddDays(30), ausente: false, vagas: 100),
            Escala(unidade.Id, codigo, StatusEscalaSisreg.Ativa, hoje.AddDays(-1), ausente: false, vagas: 200),
            Escala(unidade.Id, codigo, StatusEscalaSisreg.Ativa, hoje.AddDays(30), ausente: true, vagas: 400));
        await db.SaveChangesAsync();

        var fake = new EmbeddingsFake();
        await Catalogo(db, fake).SincronizarAsync(CancellationToken.None);

        var r = await Busca(db, fake).BuscarAsync(nome, null, 20, CancellationToken.None);
        var item = r.Itens.FirstOrDefault(i => i.Nome == nome);

        item.Should().NotBeNull();
        var executante = item!.ExecutantesInternos.FirstOrDefault(e => e.UnidadeId == unidade.Id);
        executante.Should().NotBeNull();
        executante!.VagasTotal.Should().Be(
            7,
            "só a escala ativa, vigente e presente no arquivo conta — 'Ativa' não quer dizer vigente");
    }

    private static SisregEscala Escala(
        Guid unidadeId, string codigo, StatusEscalaSisreg status, DateOnly fim, bool ausente, int vagas) =>
        new()
        {
            Id = Guid.NewGuid(),
            CodigoEscala = Random.Shared.Next(100_000_000, 999_999_999).ToString(),
            UnidadeId = unidadeId,
            Cnes = Random.Shared.Next(1_000_000, 9_999_999).ToString(),
            UnidadeNomeSisreg = "UNIDADE TESTE",
            ProfissionalCpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(),
            ProfissionalNome = "PROFISSIONAL TESTE",
            ProcedimentoCodigo = codigo,
            ProcedimentoNome = "PROCEDIMENTO TESTE",
            EhGrupo = false,
            DiaSemana = DayOfWeek.Monday,
            HoraInicio = new TimeOnly(8, 0),
            HoraFim = new TimeOnly(12, 0),
            VigenciaInicio = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10),
            VigenciaFim = fim,
            VagasTotal = vagas,
            Status = status,
            Ausente = ausente,
            VistoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
}
