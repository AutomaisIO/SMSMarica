using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Catalogo;

/// <summary>
/// Sincronismo do catálogo canônico (ADR-0052).
///
/// <para>O que estes testes prendem é o que o spike c mostrou custar caro se der errado: origem
/// que some não pode sumir do banco (solicitação antiga aponta para ela), embedding não pode ser
/// refeito de graça a cada passada (é chamada paga), e o pareamento entre sistemas nunca pode
/// virar vínculo sozinho — dois dos candidatos medidos no spike eram falsos.</para>
///
/// <para>A bancada é compartilhada: todo dado criado aqui usa sufixo aleatório, e as asserções
/// olham só as próprias linhas.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoCatalogoSyncTests(PostgresFixture fixture)
{
    private static (RegulacaoCatalogoService Servico, EmbeddingsFake Fake) Servico(SmsMaisDbContext db)
    {
        var fake = new EmbeddingsFake();
        return (new RegulacaoCatalogoService(
            db, fake, new UsuarioAtualAccessorFake(), NullLogger<RegulacaoCatalogoService>.Instance), fake);
    }

    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<SerCatalogoRecurso> RecursoSerAsync(
        SmsMaisDbContext db, string rotulo, bool ae = false)
    {
        var r = new SerCatalogoRecurso
        {
            Id = Guid.NewGuid(),
            Tipo = TipoRecursoSer.Consulta,
            Valor = Random.Shared.Next(100_000, 999_999).ToString(),
            Rotulo = rotulo,
            AmbulatorioEstadual = ae,
            SincronizadoEm = DateTime.UtcNow,
            CamposLidos = true,
        };
        db.SerCatalogoRecursos.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    private static async Task<SernitCatalogoRecurso> RecursoSernitAsync(SmsMaisDbContext db, string rotulo)
    {
        var r = new SernitCatalogoRecurso
        {
            Id = Guid.NewGuid(),
            Tipo = TipoRecursoSernit.Consulta,
            Valor = Random.Shared.Next(100_000, 999_999).ToString(),
            Rotulo = rotulo,
            SincronizadoEm = DateTime.UtcNow,
            CamposLidos = true,
        };
        db.SernitCatalogoRecursos.Add(r);
        await db.SaveChangesAsync();
        return r;
    }

    [Fact]
    public async Task Origem_nova_cria_canonico_um_para_um()
    {
        await using var db = fixture.CriarDbContext();
        var rotulo = $"CARDIOLOGIA TESTE {Sufixo()}";
        var recurso = await RecursoSerAsync(db, rotulo);

        var (servico, _) = Servico(db);
        await servico.SincronizarAsync(CancellationToken.None);

        var origem = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Include(o => o.Procedimento)
            .FirstOrDefaultAsync(o => o.SerCatalogoRecursoId == recurso.Id);

        origem.Should().NotBeNull();
        origem!.Sistema.Should().Be(SistemaRegulacao.Ser);
        origem.Ramo.Should().Be("NAO_AE");
        origem.RotuloExterno.Should().Be(rotulo);
        origem.Vinculo.Should().Be(VinculoOrigemRegulacao.Automatico);
        origem.Procedimento!.NomeCanonico.Should().Be(rotulo);
    }

    [Fact]
    public async Task Origem_que_sumiu_fica_inativa_e_nao_e_apagada()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = await RecursoSerAsync(db, $"DERMATOLOGIA TESTE {Sufixo()}");

        var (servico, _) = Servico(db);
        await servico.SincronizarAsync(CancellationToken.None);

        var origemId = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.SerCatalogoRecursoId == recurso.Id).Select(o => o.Id).FirstAsync();

        // O recurso sai do catálogo de origem — é o que acontece quando a SES recompila.
        db.SerCatalogoRecursos.Remove(await db.SerCatalogoRecursos.FirstAsync(x => x.Id == recurso.Id));
        await db.SaveChangesAsync();

        await servico.SincronizarAsync(CancellationToken.None);

        var origem = await db.RegulacaoProcedimentoOrigens.AsNoTracking().FirstOrDefaultAsync(o => o.Id == origemId);
        origem.Should().NotBeNull("a origem nunca é apagada — solicitação antiga aponta para ela");
        origem!.Ativo.Should().BeFalse();
    }

    [Fact]
    public async Task Hash_igual_nao_reembeda()
    {
        await using var db = fixture.CriarDbContext();
        await RecursoSerAsync(db, $"NEUROLOGIA TESTE {Sufixo()}");

        var (servico, fake) = Servico(db);
        await servico.SincronizarAsync(CancellationToken.None);
        var depoisDaPrimeira = fake.TextosEmbedados;
        depoisDaPrimeira.Should().BeGreaterThan(0);

        await servico.SincronizarAsync(CancellationToken.None);

        fake.TextosEmbedados.Should().Be(
            depoisDaPrimeira,
            "o hash não mudou, então a segunda passada não pode pagar embedding de novo");
    }

    [Fact]
    public async Task Falha_do_provedor_nao_derruba_o_sync_e_conta_sem_embedding()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = await RecursoSerAsync(db, $"PNEUMOLOGIA TESTE {Sufixo()}");

        var fake = new EmbeddingsFake { FalharCom = new HttpRequestException("provedor fora do ar") };
        var servico = new RegulacaoCatalogoService(
            db, fake, new UsuarioAtualAccessorFake(), NullLogger<RegulacaoCatalogoService>.Instance);

        var resultado = await servico.SincronizarAsync(CancellationToken.None);

        resultado.SemEmbedding.Should().BeGreaterThan(0);
        (await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .AnyAsync(o => o.SerCatalogoRecursoId == recurso.Id))
            .Should().BeTrue("o catálogo vale sem embedding — a busca lexical continua de pé");
    }

    [Fact]
    public async Task Timeout_do_provedor_nao_derruba_o_sync()
    {
        await using var db = fixture.CriarDbContext();
        var recurso = await RecursoSerAsync(db, $"NEFROLOGIA TESTE {Sufixo()}");

        // O caso REAL de produção (ERRO-47UCPG): `HttpClient.Timeout` lança
        // `TaskCanceledException`, que herda de `OperationCanceledException`. O filtro antigo
        // (`is not OperationCanceledException`) deixava isso escapar e derrubava o sync inteiro
        // com 500 — o teste anterior não pegava porque simulava `HttpRequestException`, que
        // passava pelo filtro.
        var fake = new EmbeddingsFake
        {
            FalharCom = new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 60 seconds elapsing."),
        };
        var servico = new RegulacaoCatalogoService(
            db, fake, new UsuarioAtualAccessorFake(), NullLogger<RegulacaoCatalogoService>.Instance);

        var resultado = await servico.SincronizarAsync(CancellationToken.None);

        resultado.SemEmbedding.Should().BeGreaterThan(0);
        (await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .AnyAsync(o => o.SerCatalogoRecursoId == recurso.Id))
            .Should().BeTrue("o catálogo tem de ficar de pé mesmo quando a Voyage demora demais");
    }

    [Fact]
    public async Task Cancelamento_de_verdade_continua_propagando()
    {
        await using var db = fixture.CriarDbContext();
        await RecursoSerAsync(db, $"HEMATOLOGIA TESTE {Sufixo()}");

        var fake = new EmbeddingsFake { FalharCom = new OperationCanceledException() };
        var servico = new RegulacaoCatalogoService(
            db, fake, new UsuarioAtualAccessorFake(), NullLogger<RegulacaoCatalogoService>.Instance);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Quem desistiu foi o chamador: engolir isso faria o sync continuar rodando depois de
        // a requisição morrer. A distinção é o `ct.IsCancellationRequested`, não o tipo.
        var acao = () => servico.SincronizarAsync(cts.Token);
        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Sugestao_so_entre_sistemas_diferentes_e_acima_do_corte()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();

        // Mesmo assunto escrito nos dois sistemas: é o par que a sugestão deve enxergar.
        var noSer = await RecursoSerAsync(db, $"OTORRINOLARINGOLOGIA {sufixo}");
        var noSernit = await RecursoSernitAsync(db, $"OTORRINOLARINGOLOGIA {sufixo}");
        // Assunto sem nada a ver, no mesmo sistema do primeiro: não pode virar sugestão.
        await RecursoSerAsync(db, $"ZZQX INEXISTENTE {sufixo}");

        var (servico, _) = Servico(db);
        await servico.SincronizarAsync(CancellationToken.None);

        var origemSer = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .FirstAsync(o => o.SerCatalogoRecursoId == noSer.Id);
        var origemSernit = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .FirstAsync(o => o.SernitCatalogoRecursoId == noSernit.Id);

        origemSer.SugeridoProcedimentoId.Should().Be(
            origemSernit.ProcedimentoId,
            "rótulos iguais em sistemas diferentes têm de ser propostos como o mesmo procedimento");
        origemSer.SugeridoScore.Should().BeGreaterThanOrEqualTo(0.85);

        // Sugerido, jamais aplicado sozinho: o vínculo continua automático e sem confirmação.
        origemSer.Vinculo.Should().Be(VinculoOrigemRegulacao.Automatico);
        origemSer.ConfirmadoEm.Should().BeNull();
        origemSer.ProcedimentoId.Should().NotBe(origemSernit.ProcedimentoId);
    }

    [Fact]
    public async Task Confirmar_move_a_origem_e_trava_contra_o_sync()
    {
        await using var db = fixture.CriarDbContext();
        var sufixo = Sufixo();
        var noSer = await RecursoSerAsync(db, $"MASTOLOGIA {sufixo}");
        var noSernit = await RecursoSernitAsync(db, $"MASTOLOGIA {sufixo}");

        var (servico, _) = Servico(db);
        await servico.SincronizarAsync(CancellationToken.None);

        var origemSer = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .FirstAsync(o => o.SerCatalogoRecursoId == noSer.Id);
        var destino = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.SernitCatalogoRecursoId == noSernit.Id).Select(o => o.ProcedimentoId).FirstAsync();

        await servico.ConfirmarPareamentoAsync(origemSer.Id, destino, CancellationToken.None);

        var depois = await db.RegulacaoProcedimentoOrigens.AsNoTracking().FirstAsync(o => o.Id == origemSer.Id);
        depois.ProcedimentoId.Should().Be(destino);
        depois.Vinculo.Should().Be(VinculoOrigemRegulacao.Confirmado);
        depois.ConfirmadoEm.Should().NotBeNull();
        depois.SugeridoProcedimentoId.Should().BeNull();

        // O canônico que ficou sem origem sai da busca, mas continua existindo.
        var orfao = await db.RegulacaoProcedimentos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == origemSer.ProcedimentoId);
        orfao.Should().NotBeNull();
        orfao!.Ativo.Should().BeFalse();
    }
}
