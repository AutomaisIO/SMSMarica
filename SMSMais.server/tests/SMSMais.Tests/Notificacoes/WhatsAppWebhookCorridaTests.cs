using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Conversas;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Corridas entre dois webhooks do MESMO número (03/10/2026, avisos …0353 e …1270): a mensagem do
/// cidadão grava antes dos manipuladores, quem perde a abertura da conversa adota a do outro, a
/// trava xmin relê e reaplica, e uma falha nos efeitos dos manipuladores não derruba o webhook nem
/// o registro do que foi enviado. A corrida é encenada por um interceptor que grava, por outro
/// DbContext, logo antes do primeiro SaveChanges do webhook.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class WhatsAppWebhookCorridaTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Conversa_aberta_por_outro_webhook_no_meio_e_adotada()
    {
        var from = NumeroAleatorio();
        var fone = TelefoneWhatsApp.Canonizar(from);
        var concorrente = Guid.CreateVersion7();
        var antes = new AntesDoPrimeiroSave(async () =>
        {
            await using var outro = fixture.CriarDbContext();
            outro.Conversas.Add(NovaConversa(concorrente, fone, naoLidas: 1));
            await outro.SaveChangesAsync();
        });

        await using (var db = CriarDbContext(antes))
            await CriarService(db).ProcessarAsync(PayloadTexto(from, "oi"));

        await using var leitura = fixture.CriarDbContext();
        var vivas = await leitura.Conversas.AsNoTracking()
            .Where(c => c.TelefoneCanonical == fone && c.ExcluidoEm == null).ToListAsync();
        var unica = Assert.Single(vivas);
        Assert.Equal(concorrente, unica.Id);
        Assert.Equal(2, unica.NaoLidas);
        var entrada = await leitura.MensagensWhatsApp.AsNoTracking().SingleAsync(m => m.Telefone == fone);
        Assert.Equal(concorrente, entrada.ConversaId);
    }

    [Fact]
    public async Task Conversa_alterada_no_meio_rele_e_reaplica_a_entrada()
    {
        var from = NumeroAleatorio();
        var fone = TelefoneWhatsApp.Canonizar(from);
        var conversaId = Guid.CreateVersion7();
        await using (var seed = fixture.CriarDbContext())
        {
            seed.Conversas.Add(NovaConversa(conversaId, fone, naoLidas: 2));
            await seed.SaveChangesAsync();
        }
        var antes = new AntesDoPrimeiroSave(async () =>
        {
            await using var outro = fixture.CriarDbContext();
            var c = await outro.Conversas.SingleAsync(x => x.Id == conversaId);
            c.NaoLidas += 1; // o outro webhook gravou a mensagem dele
            await outro.SaveChangesAsync();
        });

        await using (var db = CriarDbContext(antes))
            await CriarService(db).ProcessarAsync(PayloadTexto(from, "oi"));

        await using var leitura = fixture.CriarDbContext();
        var conversa = await leitura.Conversas.AsNoTracking().SingleAsync(c => c.Id == conversaId);
        Assert.Equal(4, conversa.NaoLidas); // 2 + a do outro + a nossa — nenhuma perdida
        Assert.True(await leitura.MensagensWhatsApp.AnyAsync(m => m.Telefone == fone && m.ConversaId == conversaId));
    }

    [Fact]
    public async Task Efeito_do_manipulador_que_nao_grava_nao_derruba_o_webhook_nem_o_registro_do_envio()
    {
        var from = NumeroAleatorio();
        var fone = TelefoneWhatsApp.Canonizar(from);
        // Manipulador que "respondeu" (registro da saída pendente) e deixou um efeito inválido:
        // uma segunda conversa viva do mesmo número (viola IX_conversa_telefone_canonical_canal).
        var manipulador = new ManipuladorDeTeste((ctx, db) =>
        {
            db.MensagensWhatsApp.Add(new MensagemWhatsApp
            {
                Id = Guid.CreateVersion7(),
                Telefone = fone,
                Direcao = DirecaoMensagem.Saida,
                Conteudo = "resposta que saiu",
                Status = StatusMensagemWhatsApp.Enviada,
                WaMessageId = $"wamid.SAIDA.{Guid.NewGuid():N}",
                OcorridoEm = DateTime.UtcNow,
                CriadoEm = DateTime.UtcNow,
            });
            db.Conversas.Add(NovaConversa(Guid.CreateVersion7(), fone, naoLidas: 0));
        });

        await using (var db = CriarDbContext(interceptor: null))
        {
            manipulador.Db = db;
            await CriarService(db, manipulador).ProcessarAsync(PayloadTexto(from, "oi"));
        }

        await using var leitura = fixture.CriarDbContext();
        Assert.Single(await leitura.Conversas.AsNoTracking()
            .Where(c => c.TelefoneCanonical == fone && c.ExcluidoEm == null).ToListAsync());
        Assert.True(await leitura.MensagensWhatsApp.AnyAsync(m => m.Telefone == fone && m.Direcao == DirecaoMensagem.Entrada));
        Assert.True(await leitura.MensagensWhatsApp.AnyAsync(m => m.Telefone == fone && m.Direcao == DirecaoMensagem.Saida));
    }

    // ---------------------------------------------------------------------------------------

    private SmsMaisDbContext CriarDbContext(IInterceptor? interceptor)
    {
        var builder = new DbContextOptionsBuilder<SmsMaisDbContext>()
            .UseNpgsql(fixture.ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(SmsMaisDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__migrations", SmsMaisDbContext.SchemaPadrao);
                npgsql.UseVector();
            });
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new SmsMaisDbContext(builder.Options);
    }

    private static WhatsAppWebhookService CriarService(
        SmsMaisDbContext db, params IManipuladorMensagemWhatsApp[] manipuladores) => new(
        db,
        Substitute.For<IPacientesService>(),
        Substitute.For<IConversaNotificador>(),
        manipuladores,
        Options.Create(new ComunicacaoPacienteOptions()),
        Substitute.For<IContatoComprometidoService>(),
        NullLogger<WhatsAppWebhookService>.Instance);

    private static Conversa NovaConversa(Guid id, string fone, int naoLidas) => new()
    {
        Id = id,
        Canal = CanalConversa.WhatsApp,
        TelefoneCanonical = fone,
        Status = StatusConversa.Aberta,
        NaoLidas = naoLidas,
        JanelaExpiraEm = DateTime.UtcNow.AddHours(23),
        PrimeiroContatoEm = DateTime.UtcNow,
        CriadoEm = DateTime.UtcNow,
    };

    private static string NumeroAleatorio() => $"55219{Random.Shared.Next(10_000_000, 99_999_999)}";

    private static string PayloadTexto(string from, string texto)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return "{\"entry\":[{\"changes\":[{\"value\":{\"messaging_product\":\"whatsapp\",\"messages\":[" +
               $"{{\"id\":\"wamid.TEST.{Guid.NewGuid():N}\",\"from\":\"{from}\",\"timestamp\":\"{ts}\"," +
               $"\"type\":\"text\",\"text\":{{\"body\":\"{texto}\"}}}}" +
               "]}}]}]}";
    }

    /// <summary>Roda <paramref name="acao"/> uma única vez, logo antes do primeiro SaveChanges.</summary>
    private sealed class AntesDoPrimeiroSave(Func<Task> acao) : SaveChangesInterceptor
    {
        private bool _feito;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_feito)
            {
                _feito = true;
                await acao();
            }
            return result;
        }
    }

    private sealed class ManipuladorDeTeste(Action<ManipuladorContexto, SmsMaisDbContext> efeito) : IManipuladorMensagemWhatsApp
    {
        public SmsMaisDbContext? Db { get; set; }
        public int Ordem => 1;

        public Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
        {
            efeito(ctx, Db!);
            return Task.CompletedTask;
        }
    }
}
