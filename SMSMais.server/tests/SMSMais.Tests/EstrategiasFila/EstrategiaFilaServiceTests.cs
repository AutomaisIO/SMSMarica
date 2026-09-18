using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using SMSMais.Core.AgendaRegulacao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EstrategiasFila;
using SMSMais.Core.EstrategiasFila.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.EstrategiasFila;

/// <summary>
/// O ciclo da estratégia com o agente dublado: criar, rerodar (manual e agente), falha do agente
/// que fica registrada sem virar "atual", aplicar, arquivar.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EstrategiaFilaServiceTests(PostgresFixture fixture)
{
    private static readonly Guid Operador = Guid.NewGuid();

    private static (EstrategiaFilaService Servico, IEstrategiaAgenteIa Agente) Criar(SmsMaisDbContext db)
    {
        var cenarios = new CenarioFilaService(
            db, new AgendaDemandaService(db, new UsuarioAtualAccessorFake()), new MemoryCache(new MemoryCacheOptions()));
        var agente = Substitute.For<IEstrategiaAgenteIa>();
        return (new EstrategiaFilaService(db, cenarios, agente, new UsuarioAtualAccessorFake(Operador)), agente);
    }

    [Fact]
    public async Task Criar_grava_a_rodada_1_manual_com_o_cenario_de_agora()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);
        var (servico, _) = Criar(db);

        var id = await servico.CriarAsync(new CriarEstrategiaRequest(
            "Zerar a fila de teste", seed.CodigoGrupo, seed.NomeGrupo, null!));

        var e = await servico.ObterAsync(id);
        e.Status.Should().Be(StatusEstrategiaFila.Rascunho);
        e.CriadoPor.Should().Be(Operador);
        e.Rodadas.Should().ContainSingle().Which.Modo.Should().Be(ModoRodadaEstrategia.Manual);
        e.RodadaAtual.Should().NotBeNull();
        e.RodadaAtual!.Numero.Should().Be(1);
        e.RodadaAtual.Cenario.Fila.Total.Should().Be(SeedEstrategiasFila.NaFila);
        e.RodadaAtual.Projecao.FilaInicial.Should().Be(SeedEstrategiasFila.NaFila);
        e.Parametros.Should().BeEquivalentTo(e.RodadaAtual.ParametrosResultado);

        // A lista de procedimentos passa a marcar "tem estratégia".
        var lista = await new CenarioFilaService(
                db, new AgendaDemandaService(db, new UsuarioAtualAccessorFake()), new MemoryCache(new MemoryCacheOptions()))
            .ListarProcedimentosAsync(seed.NomeGrupo, null);
        lista.Should().ContainSingle(p => p.Codigo == seed.CodigoGrupo).Which.TemEstrategia.Should().BeTrue();
    }

    [Fact]
    public async Task Rodada_do_agente_preenche_os_parametros_e_vira_a_atual_e_falha_fica_registrada_sem_virar()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);
        var (servico, agente) = Criar(db);
        var id = await servico.CriarAsync(new CriarEstrategiaRequest("Agente", seed.CodigoGrupo, seed.NomeGrupo, null!));
        var antes = (await servico.ObterAsync(id)).Parametros;

        // O agente dobra os profissionais e propõe ações.
        agente.PlanejarAsync(Arg.Any<CenarioFilaDto>(), Arg.Any<ParametrosEstrategia>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var p = ci.ArgAt<ParametrosEstrategia>(1);
                var finais = p with { Profissionais = p.Profissionais.Com(p.Profissionais.Valor * 2) };
                var proj = SimuladorFila.Projetar(finais, ci.ArgAt<CenarioFilaDto>(0).Fila.Total);
                return new ResultadoAgenteEstrategia(
                    finais, proj,
                    new PropostaAgenteDto("Dobrar a equipe.", [new AcaoPropostaDto("profissional", "Habilitar mais um médico", null, 20)], ["Depende de contratação"], 0.7, 3),
                    "claude-opus-4-8", 12_000, 900, 4_000, Falha: null);
            });

        var rodada = await servico.RodarAsync(id, new NovaRodadaRequest(ModoRodadaEstrategia.Agente, antes));

        rodada.Numero.Should().Be(2);
        rodada.Modo.Should().Be(ModoRodadaEstrategia.Agente);
        rodada.Proposta.Should().NotBeNull();
        rodada.Proposta!.Acoes.Should().ContainSingle();
        rodada.ParametrosResultado.Profissionais.Valor.Should().Be(antes.Profissionais.Valor * 2);
        rodada.CustoUsd.Should().BeGreaterThan(0);
        rodada.Falha.Should().BeNull();

        var depois = await servico.ObterAsync(id);
        depois.RodadaAtual!.Numero.Should().Be(2);
        depois.Parametros.Profissionais.Valor.Should().Be(antes.Profissionais.Valor * 2);
        depois.Rodadas.Should().HaveCount(2);

        // Agente falha: a rodada 3 existe (com o custo), mas a atual continua a 2.
        agente.PlanejarAsync(Arg.Any<CenarioFilaDto>(), Arg.Any<ParametrosEstrategia>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoAgenteEstrategia(null, null, null, "claude-opus-4-8", 5_000, 100, 2_000, "sem terminal"));

        var falhou = await servico.RodarAsync(id, new NovaRodadaRequest(ModoRodadaEstrategia.Agente, null!));
        falhou.Numero.Should().Be(3);
        falhou.Falha.Should().Be("sem terminal");
        falhou.Proposta.Should().BeNull();

        var final = await servico.ObterAsync(id);
        final.RodadaAtual!.Numero.Should().Be(2);
        final.Rodadas.Should().HaveCount(3);
        (await servico.ObterRodadaAsync(id, 3)).Falha.Should().Be("sem terminal");
    }

    [Fact]
    public async Task Rodada_manual_com_parametros_novos_vira_a_atual()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);
        var (servico, _) = Criar(db);
        var id = await servico.CriarAsync(new CriarEstrategiaRequest("Manual", seed.CodigoGrupo, seed.NomeGrupo, null!));
        var p = (await servico.ObterAsync(id)).Parametros;

        var novos = p with { Profissionais = p.Profissionais.Com(4), Mutiroes = [new MutiraoDto(1, 100, "sábado")] };
        var rodada = await servico.RodarAsync(id, new NovaRodadaRequest(ModoRodadaEstrategia.Manual, novos));

        rodada.Numero.Should().Be(2);
        rodada.ParametrosResultado.Profissionais.Valor.Should().Be(4);
        rodada.Projecao.Serie[1].Capacidade.Should().BeGreaterThan(rodada.Projecao.CapacidadeSemanal);
        (await servico.ObterAsync(id)).RodadaAtual!.Numero.Should().Be(2);
    }

    [Fact]
    public async Task Aplicar_arquivar_e_excluir_seguem_o_ciclo()
    {
        await using var db = fixture.CriarDbContext();
        var seed = await SeedEstrategiasFila.CriarAsync(db);
        var (servico, _) = Criar(db);
        var id = await servico.CriarAsync(new CriarEstrategiaRequest("Ciclo", seed.CodigoGrupo, seed.NomeGrupo, null!));

        await servico.AtualizarAsync(id, new AtualizarEstrategiaRequest(
            "Ciclo pronto", (await servico.ObterAsync(id)).Parametros, StatusEstrategiaFila.Pronta));
        (await servico.ObterAsync(id)).Status.Should().Be(StatusEstrategiaFila.Pronta);

        await servico.MarcarAplicadaAsync(id, new MarcarAplicadaRequest("Escala aberta no SISREG pela regulação."));
        var aplicada = await servico.ObterAsync(id);
        aplicada.Status.Should().Be(StatusEstrategiaFila.Aplicada);
        aplicada.AplicadaEm.Should().NotBeNull();
        aplicada.AplicadaPor.Should().Be(Operador);
        aplicada.AplicacaoNota.Should().Contain("SISREG");

        await servico.ArquivarAsync(id);
        (await servico.ObterAsync(id)).Status.Should().Be(StatusEstrategiaFila.Arquivada);
        await FluentActions.Awaiting(() => servico.RodarAsync(id, new NovaRodadaRequest(ModoRodadaEstrategia.Manual, null!)))
            .Should().ThrowAsync<ConflitoException>();

        (await servico.ListarAsync(new EstrategiaFiltro(null, seed.CodigoGrupo, null))).Should().BeEmpty();
        (await servico.ListarAsync(new EstrategiaFiltro(null, seed.CodigoGrupo, null, IncluirArquivadas: true)))
            .Should().ContainSingle().Which.Status.Should().Be(StatusEstrategiaFila.Arquivada);

        await servico.ExcluirAsync(id);
        await FluentActions.Awaiting(() => servico.ObterAsync(id)).Should().ThrowAsync<NaoEncontradoException>();
    }
}
