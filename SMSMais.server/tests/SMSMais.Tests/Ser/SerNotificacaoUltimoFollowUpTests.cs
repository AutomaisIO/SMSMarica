using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Regulacao.FollowUp;
using SMSMais.Core.Ser;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Ser;

/// <summary>
/// Notificações do SER trazem o <b>último FollowUP</b> da solicitação em cada linha — em qualquer
/// tipo de gatilho, não só no de "FollowUP novo".
///
/// Precisa de Postgres real: o que está sob teste é a tradução da subconsulta correlacionada
/// (<c>ILIKE</c> + <c>ORDER BY … LIMIT 1</c> projetada num record) para SQL. Em memória passaria
/// e em produção poderia quebrar.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerNotificacaoUltimoFollowUpTests(PostgresFixture fixture)
{
    private static SerNotificacaoService CriarService(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake());

    private static SerSolicitacao Semear(SmsMaisDbContext db, string idSer, SituacaoSer situacao)
    {
        var agora = DateTime.UtcNow;
        var s = new SerSolicitacao
        {
            Id = Guid.CreateVersion7(),
            IdSer = idSer,
            Tipo = TipoRecursoSer.Consulta,
            Recurso = "CARDIOLOGIA",
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = situacao,
            SincronizadoEm = agora,
            CriadoEm = agora,
        };
        db.SerSolicitacoes.Add(s);
        db.SerGatilhos.Add(new SerGatilho
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            IdSer = idSer,
            Tipo = TipoGatilhoSer.MudancaSituacao,
            ChaveEvento = situacao.ToString(),
            SituacaoAnterior = SituacaoSer.EmFila,
            SituacaoAtual = situacao,
            CriadoEm = agora,
        });
        return s;
    }

    private static void Evento(SmsMaisDbContext db, SerSolicitacao s, string verbo,
        DateTime quando, string? usuario, string? observacao, string? categoria = null) =>
        db.SerEventos.Add(new SerEvento
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            DataEvento = quando,
            Evento = verbo,
            // O que o sincronizador faz na captura: verbo tipado uma vez, texto cru ao lado.
            TipoEvento = ClassificadorEventoRegulacao.TipoDoVerbo(verbo),
            FollowUpCategoria = categoria,
            Usuario = usuario,
            Observacao = observacao,
            CapturadoEm = DateTime.UtcNow,
        });

    [Fact]
    public async Task Traz_o_followup_mais_recente_e_ignora_os_outros_verbos()
    {
        await using var db = fixture.CriarDbContext();
        var idSer = "T" + Random.Shared.Next(100000, 999999);
        var s = Semear(db, idSer, SituacaoSer.Agendada);

        var t0 = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        Evento(db, s, "Solicitar", t0, "MEDICO", "pedido inicial");
        Evento(db, s, "FollowUP", t0.AddDays(1), "OPERADOR A", "primeira tentativa de contato");
        // Grafia com hífen: o sincronizador tolera, a listagem tem de tolerar também.
        Evento(db, s, "Follow-UP", t0.AddDays(3), "OPERADOR B", "SEM CONTATO: caixa postal", "FalhaContato");
        Evento(db, s, "Agendar", t0.AddDays(5), "CENTRAL", "agendado para 20/09");
        await db.SaveChangesAsync();

        var pagina = await CriarService(db).ListarAsync(
            new SerNotificacaoFiltroDto { Tamanho = 1000 }, CancellationToken.None);

        var linha = Assert.Single(pagina.Itens, n => n.IdSer == idSer);
        Assert.NotNull(linha.UltimoFollowUp);
        Assert.Equal("OPERADOR B", linha.UltimoFollowUp!.Usuario);
        Assert.Equal("SEM CONTATO: caixa postal", linha.UltimoFollowUp.Observacao);
        Assert.Equal(t0.AddDays(3), linha.UltimoFollowUp.DataEvento);
        Assert.Equal("FalhaContato", linha.UltimoFollowUp.Categoria);
    }

    [Fact]
    public async Task Filtro_por_categoria_olha_o_ultimo_followup_e_nao_a_trilha_inteira()
    {
        await using var db = fixture.CriarDbContext();
        var t0 = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        // Falha de contato foi o ÚLTIMO FollowUP: entra no filtro.
        var idFalha = "T" + Random.Shared.Next(100000, 999999);
        var a = Semear(db, idFalha, SituacaoSer.EmFila);
        Evento(db, a, "FollowUP", t0, "X", "contato realizado", "ContatoRealizado");
        Evento(db, a, "FollowUP", t0.AddDays(1), "X", "sem contato", "FalhaContato");

        // Teve falha de contato, mas o último FollowUP já é contato realizado: fica de fora.
        var idResolvido = "T" + Random.Shared.Next(100000, 999999);
        var b = Semear(db, idResolvido, SituacaoSer.EmFila);
        Evento(db, b, "FollowUP", t0, "X", "sem contato", "FalhaContato");
        Evento(db, b, "FollowUP", t0.AddDays(1), "X", "contato realizado", "ContatoRealizado");

        // Nunca teve FollowUP: fica de fora.
        var idSem = "T" + Random.Shared.Next(100000, 999999);
        Semear(db, idSem, SituacaoSer.EmFila);
        await db.SaveChangesAsync();

        var pagina = await CriarService(db).ListarAsync(
            new SerNotificacaoFiltroDto { CategoriaFollowUp = "FalhaContato", Tamanho = 1000 },
            CancellationToken.None);

        Assert.Contains(pagina.Itens, n => n.IdSer == idFalha);
        Assert.DoesNotContain(pagina.Itens, n => n.IdSer == idResolvido);
        Assert.DoesNotContain(pagina.Itens, n => n.IdSer == idSem);
    }

    [Fact]
    public async Task Sem_followup_a_linha_vem_com_null_e_nao_some_da_fila()
    {
        await using var db = fixture.CriarDbContext();
        var idSer = "T" + Random.Shared.Next(100000, 999999);
        var s = Semear(db, idSer, SituacaoSer.Cancelada);
        Evento(db, s, "Solicitar", DateTime.UtcNow.AddDays(-2), "MEDICO", null);
        Evento(db, s, "Cancelar", DateTime.UtcNow.AddDays(-1), "CENTRAL", "duplicidade");
        await db.SaveChangesAsync();

        var pagina = await CriarService(db).ListarAsync(
            new SerNotificacaoFiltroDto { Tamanho = 1000 }, CancellationToken.None);

        var linha = Assert.Single(pagina.Itens, n => n.IdSer == idSer);
        Assert.Null(linha.UltimoFollowUp);
    }
}
