using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.FollowUp;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.FollowUp;

/// <summary>
/// Classificação PERSISTIDA do FollowUP: o lote pega o que não tem categoria ou tem hash de regras
/// diferente do atual, grava categoria + hash, e não toca no que já está em dia. Precisa de
/// Postgres real — o filtro do lote é o índice parcial <c>tipo_evento = 2</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FollowUpClassificacaoServiceTests(PostgresFixture fixture)
{
    private static FollowUpClassificacaoService Servico(SmsMaisDbContext db)
    {
        var config = new RegulacaoConfiguracaoService(
            db, new MemoryCache(new MemoryCacheOptions()), new UsuarioAtualAccessorFake());
        return new FollowUpClassificacaoService(db, config, NullLogger<FollowUpClassificacaoService>.Instance);
    }

    /// <summary>A configuração padrão nasce com regras VAZIAS ("[]"); a semente entra pela tela.
    /// O teste carrega a semente direto para classificar de verdade.</summary>
    private static async Task CarregarSementeAsync(SmsMaisDbContext db)
    {
        var c = await db.RegulacaoConfiguracoes.FirstOrDefaultAsync(x => x.Id == RegulacaoConfiguracao.IdSingleton);
        if (c is null)
        {
            c = new RegulacaoConfiguracao { Id = RegulacaoConfiguracao.IdSingleton };
            db.RegulacaoConfiguracoes.Add(c);
        }

        c.RegrasFollowupJson = SementeFollowUp.Json;
        await db.SaveChangesAsync();
    }

    private static async Task<SerEvento> SemearFollowUpAsync(
        SmsMaisDbContext db, string observacao, string? hashGravado = null, string? categoriaGravada = null)
    {
        var agora = DateTime.UtcNow;
        var s = new SerSolicitacao
        {
            Id = Guid.CreateVersion7(),
            IdSer = "C" + Random.Shared.Next(100000, 999999),
            Tipo = TipoRecursoSer.Consulta,
            Recurso = "ORTOPEDIA",
            PacienteNome = "PACIENTE DE TESTE",
            Situacao = SituacaoSer.EmFila,
            SincronizadoEm = agora,
            CriadoEm = agora,
        };
        var e = new SerEvento
        {
            Id = Guid.CreateVersion7(),
            SerSolicitacaoId = s.Id,
            DataEvento = agora,
            Evento = "FollowUP",
            TipoEvento = TipoEventoExterno.FollowUp,
            Observacao = observacao,
            FollowUpRegrasHash = hashGravado,
            FollowUpCategoria = categoriaGravada,
            CapturadoEm = agora,
        };
        db.SerSolicitacoes.Add(s);
        db.SerEventos.Add(e);
        await db.SaveChangesAsync();
        return e;
    }

    [Fact]
    public async Task Lote_classifica_o_que_nao_tem_categoria_e_grava_o_hash_das_regras()
    {
        await using var db = fixture.CriarDbContext();
        await CarregarSementeAsync(db);
        var evento = await SemearFollowUpAsync(db, "SEM CONTATO: DIVERSAS TENTATIVAS, CAIXA POSTAL");

        var svc = Servico(db);
        // Drena até zerar: outros testes podem ter deixado FollowUPs pendentes na bancada.
        while ((await svc.ClassificarPendentesAsync(500, CancellationToken.None)).Total > 0) { }

        await using var leitura = fixture.CriarDbContext();
        var lido = await leitura.SerEventos.SingleAsync(x => x.Id == evento.Id);
        Assert.Equal("FalhaContato", lido.FollowUpCategoria);
        Assert.Equal(ClassificadorEventoRegulacao.HashDasRegras(SementeFollowUp.Json), lido.FollowUpRegrasHash);
    }

    [Fact]
    public async Task Regras_diferentes_reclassificam_e_regras_iguais_nao_tocam()
    {
        await using var db = fixture.CriarDbContext();
        await CarregarSementeAsync(db);
        var hashAtual = ClassificadorEventoRegulacao.HashDasRegras(SementeFollowUp.Json);

        // Classificado por regras antigas (hash diferente) com categoria que hoje seria outra.
        var defasado = await SemearFollowUpAsync(db, "PACIENTE INFORMOU QUE ESTA CIENTE DA CONSULTA",
            hashGravado: "0000000000000000", categoriaGravada: "Outro");
        // Já em dia: não pode ser tocado, mesmo que a categoria gravada pareça "errada".
        var emDia = await SemearFollowUpAsync(db, "SEM CONTATO",
            hashGravado: hashAtual, categoriaGravada: "MarcaDeQueNaoFoiTocado");

        var svc = Servico(db);
        while ((await svc.ClassificarPendentesAsync(500, CancellationToken.None)).Total > 0) { }

        await using var leitura = fixture.CriarDbContext();
        var d = await leitura.SerEventos.SingleAsync(x => x.Id == defasado.Id);
        var e = await leitura.SerEventos.SingleAsync(x => x.Id == emDia.Id);

        Assert.Equal("ContatoRealizado", d.FollowUpCategoria);
        Assert.Equal(hashAtual, d.FollowUpRegrasHash);
        Assert.Equal("MarcaDeQueNaoFoiTocado", e.FollowUpCategoria);

        var pendentes = await svc.ContarPendentesAsync(CancellationToken.None);
        Assert.Equal(0, pendentes.Total);
    }

    [Fact]
    public async Task Classificar_na_captura_usa_as_regras_atuais()
    {
        await using var db = fixture.CriarDbContext();
        await CarregarSementeAsync(db);

        var r = await Servico(db).ClassificarAsync("Aguardando vaga, paciente segue em fila de espera", CancellationToken.None);

        Assert.Equal("SemVaga", r.Categoria);
        Assert.Equal(ClassificadorEventoRegulacao.HashDasRegras(SementeFollowUp.Json), r.RegrasHash);
    }
}
