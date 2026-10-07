using SMSMais.Core.Regulacao.FollowUp;
using SMSMais.Core.Regulacao.Notificacoes;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Ser;

/// <summary>
/// Filtro por <b>técnico regulador</b> nas Notificações do SER: o técnico é o usuário do
/// "Solicitar" mais antigo da trilha, com a caixa normalizada.
///
/// Precisa de Postgres real: o técnico é subconsulta correlacionada dentro de uma projeção que
/// depois é filtrada e AGRUPADA — é exatamente o tipo de composição que passa em memória e quebra
/// na tradução para SQL.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerNotificacaoTecnicoTests(PostgresFixture fixture)
{
    private static SerNotificacaoService CriarService(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake());

    private static string NovoId() => "T" + Random.Shared.Next(100000, 999999);

    /// <summary>Nome único por teste: a base de testes é compartilhada e acumula linhas.</summary>
    private static string NovoTecnico() => "tecnico " + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Recurso único por teste: a base é compartilhada e acumula linhas.</summary>
    private static string NovoRecurso() => "RECURSO " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static SerSolicitacao Semear(
        SmsMaisDbContext db, string idSer, TipoRecursoSer tipo = TipoRecursoSer.Consulta, string recurso = "CARDIOLOGIA")
    {
        var agora = DateTime.UtcNow;
        var s = new SerSolicitacao
        {
            Id = Guid.CreateVersion7(),
            IdSer = idSer,
            Tipo = tipo,
            Recurso = recurso,
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = SituacaoSer.EmFila,
            SincronizadoEm = agora,
            CriadoEm = agora,
        };
        db.SerSolicitacoes.Add(s);
        db.SerGatilhos.Add(new SerGatilho
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            IdSer = idSer,
            Tipo = TipoGatilhoSer.NovaSolicitacao,
            ChaveEvento = "nova",
            SituacaoAtual = SituacaoSer.EmFila,
            CriadoEm = agora,
        });
        return s;
    }

    private static void Evento(SmsMaisDbContext db, SerSolicitacao s, string verbo, DateTime quando, string? usuario) =>
        db.SerEventos.Add(new SerEvento
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            DataEvento = quando,
            Evento = verbo,
            TipoEvento = ClassificadorEventoRegulacao.TipoDoVerbo(verbo),
            Usuario = usuario,
            CapturadoEm = DateTime.UtcNow,
        });

    [Fact]
    public async Task Tecnico_e_o_primeiro_solicitar_normalizado_e_o_filtro_ignora_a_caixa()
    {
        await using var db = fixture.CriarDbContext();
        var t0 = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var tecnico = NovoTecnico();
        var outro = NovoTecnico();

        // Reenviada por outra pessoa depois: continua sendo de quem incluiu primeiro.
        var idA = NovoId();
        var a = Semear(db, idA);
        Evento(db, a, "Solicitar", t0.AddDays(2), outro);
        Evento(db, a, "Solicitar", t0, "  " + tecnico + " ");
        Evento(db, a, "FollowUP", t0.AddDays(3), outro);

        var idB = NovoId();
        var b = Semear(db, idB);
        Evento(db, b, "Solicitar", t0, outro);

        // Histórico ainda não lido: sem técnico.
        var idSem = NovoId();
        Semear(db, idSem);
        await db.SaveChangesAsync();

        var service = CriarService(db);
        var todas = await service.ListarAsync(new SerNotificacaoFiltroDto { Tamanho = 1000 }, CancellationToken.None);
        Assert.Equal(tecnico.ToUpperInvariant(), Assert.Single(todas.Itens, n => n.IdSer == idA).Tecnico);
        Assert.Null(Assert.Single(todas.Itens, n => n.IdSer == idSem).Tecnico);

        // A chave chega em minúsculas: o filtro normaliza.
        var filtradas = await service.ListarAsync(
            new SerNotificacaoFiltroDto { Tecnicos = [tecnico], Tamanho = 1000 }, CancellationToken.None);
        Assert.Contains(filtradas.Itens, n => n.IdSer == idA);
        Assert.DoesNotContain(filtradas.Itens, n => n.IdSer == idB);
        Assert.DoesNotContain(filtradas.Itens, n => n.IdSer == idSem);

        // Mais de um técnico + "sem técnico".
        var varios = await service.ListarAsync(
            new SerNotificacaoFiltroDto { Tecnicos = [outro.ToUpperInvariant(), TecnicoInclusao.SemTecnico], Tamanho = 1000 },
            CancellationToken.None);
        Assert.Contains(varios.Itens, n => n.IdSer == idB);
        Assert.Contains(varios.Itens, n => n.IdSer == idSem);
        Assert.DoesNotContain(varios.Itens, n => n.IdSer == idA);
    }

    [Fact]
    public async Task Resumo_e_lista_de_tecnicos_recontam_pelo_filtro()
    {
        await using var db = fixture.CriarDbContext();
        var t0 = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var tecnico = NovoTecnico();
        var soReenviou = NovoTecnico();

        var a = Semear(db, NovoId(), TipoRecursoSer.Consulta);
        Evento(db, a, "Solicitar", t0, tecnico);
        var b = Semear(db, NovoId(), TipoRecursoSer.Exame);
        Evento(db, b, "Solicitar", t0, tecnico.ToUpperInvariant());
        Evento(db, b, "Solicitar", t0.AddDays(1), soReenviou);
        await db.SaveChangesAsync();

        var service = CriarService(db);

        var resumo = await service.ResumoAsync([tecnico], null, CancellationToken.None);
        Assert.Equal(2, resumo.Total);
        Assert.Equal(1, resumo.Contadores.Single(c => c.Tipo == TipoRecursoSer.Exame).Quantidade);

        var tecnicos = await service.TecnicosAsync(CancellationToken.None);
        // Mesma pessoa em caixas diferentes = um técnico só.
        Assert.Equal(2, Assert.Single(tecnicos, t => t.Chave == tecnico.ToUpperInvariant()).Pendentes);
        // Quem só reenviou aparece no filtro, sem pendência atribuída.
        Assert.Equal(0, Assert.Single(tecnicos, t => t.Chave == soReenviou.ToUpperInvariant()).Pendentes);
    }

    [Fact]
    public async Task Filtro_por_recurso_recorta_lista_resumo_e_opcoes()
    {
        await using var db = fixture.CriarDbContext();
        var recursoA = NovoRecurso();
        var recursoB = NovoRecurso();

        var idA1 = NovoId();
        Semear(db, idA1, TipoRecursoSer.Consulta, recursoA);
        var idA2 = NovoId();
        Semear(db, idA2, TipoRecursoSer.Exame, recursoA);
        var idB = NovoId();
        Semear(db, idB, TipoRecursoSer.Consulta, recursoB);
        await db.SaveChangesAsync();

        var service = CriarService(db);

        // Lista: marcar um recurso recorta só o dele (os dois tipos daquele recurso).
        var soA = await service.ListarAsync(
            new SerNotificacaoFiltroDto { Recursos = [recursoA], Tamanho = 1000 }, CancellationToken.None);
        Assert.Contains(soA.Itens, n => n.IdSer == idA1);
        Assert.Contains(soA.Itens, n => n.IdSer == idA2);
        Assert.DoesNotContain(soA.Itens, n => n.IdSer == idB);

        // Resumo reconta pelo recurso: 2 do recursoA, 1 em cada tipo.
        var resumo = await service.ResumoAsync(null, [recursoA], CancellationToken.None);
        Assert.Equal(2, resumo.Total);
        Assert.Equal(1, resumo.Contadores.Single(c => c.Tipo == TipoRecursoSer.Exame).Quantidade);

        // Opções do filtro: cada recurso com sua contagem de pendentes.
        var recursos = await service.RecursosAsync(CancellationToken.None);
        Assert.Equal(2, Assert.Single(recursos, r => r.Recurso == recursoA).Pendentes);
        Assert.Equal(1, Assert.Single(recursos, r => r.Recurso == recursoB).Pendentes);
    }
}
