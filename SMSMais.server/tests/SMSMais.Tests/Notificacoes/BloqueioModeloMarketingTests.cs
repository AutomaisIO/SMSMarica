using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.PendenciasCadastro;
using SMSMais.Core.Tfd.Configuracao;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// O BREAK da plataforma (23/10/2026): tudo o que sai pelo SMSMais é utilidade. Modelo que a Meta
/// classificou como MARKETING não sai — venha de onde vier — e não chega nem a ser pedido ao relay.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BloqueioModeloMarketingTests(PostgresFixture fixture)
{
    private const string Catalogo = """
        {
          "templates": [
            { "nome": "aviso_util", "idioma": "pt_BR", "categoria": "UTILITY",
              "corpo": "Olá {{1}}.", "parametros": 1, "exemplos": ["Maria"] },
            { "nome": "aviso_promo", "idioma": "pt_BR", "categoria": "MARKETING",
              "corpo": "Olá {{1}}.", "parametros": 1, "exemplos": ["Maria"] }
          ]
        }
        """;

    [Fact]
    public void Catalogo_le_a_categoria_e_a_trava_so_barra_o_marketing()
    {
        var modelos = WhatsAppCliente.ParsearTemplates(Catalogo);

        Assert.True(modelos.Single(m => m.Nome == "aviso_promo").Marketing);
        Assert.False(modelos.Single(m => m.Nome == "aviso_util").Marketing);
        Assert.StartsWith(WhatsAppCliente.CodigoModeloMarketing,
            WhatsAppCliente.MotivoBloqueioMarketing(modelos, "AVISO_PROMO"));
        Assert.Null(WhatsAppCliente.MotivoBloqueioMarketing(modelos, "aviso_util"));
        Assert.Null(WhatsAppCliente.MotivoBloqueioMarketing(modelos, "fora_do_catalogo"));
    }

    [Fact]
    public async Task Modelo_marketing_nao_chega_ao_relay_e_fica_registrado_como_bloqueado()
    {
        await using var db = fixture.CriarDbContext();
        var relay = new RelayFalso(Catalogo);
        var cliente = Cliente(db, relay);
        var telefone = SeedSolicitacao.TelefoneAleatorio();

        var promo = await cliente.EnviarTemplateAsync(telefone, "aviso_promo", "pt_BR", ["Maria"]);
        var util = await cliente.EnviarTemplateAsync(telefone, "aviso_util", "pt_BR", ["Maria"]);

        Assert.False(promo.Ok);
        Assert.StartsWith(WhatsAppCliente.CodigoModeloMarketing, promo.Erro);
        // Só o de utilidade foi pedido ao relay.
        var envios = relay.Envios;
        Assert.Single(envios);
        Assert.Contains("aviso_util", envios[0]);
        Assert.DoesNotContain(envios, e => e.Contains("aviso_promo"));

        var registro = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Template == "aviso_promo" && m.Telefone.EndsWith(telefone.Substring(telefone.Length - 8)))
            .ToListAsync();
        var bloqueada = Assert.Single(registro);
        Assert.Equal(StatusMensagemWhatsApp.Falha, bloqueada.Status);
        Assert.StartsWith("[BLOQUEADO — MARKETING]", bloqueada.Conteudo);
    }

    [Fact]
    public async Task Sem_catalogo_agora_a_trava_usa_o_ultimo_conhecido()
    {
        await using var db = fixture.CriarDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var relay = new RelayFalso(Catalogo);
        var cliente = Cliente(db, relay, cache);
        await cliente.ListarTemplatesAsync(); // o relay respondeu uma vez

        relay.CatalogoForaDoAr = true;
        cache.Remove("whatsapp:templates"); // passou o cache curto

        var r = await cliente.EnviarTemplateAsync(SeedSolicitacao.TelefoneAleatorio(), "aviso_promo", "pt_BR", ["Maria"]);

        Assert.False(r.Ok);
        Assert.StartsWith(WhatsAppCliente.CodigoModeloMarketing, r.Erro);
        Assert.Empty(relay.Envios);
    }

    private static WhatsAppCliente Cliente(
        Data.SmsMaisDbContext db, RelayFalso relay, IMemoryCache? cache = null)
    {
        var config = Substitute.For<ITfdConfigService>();
        config.ObterWhatsAppContextoAsync(Arg.Any<CancellationToken>())
            .Returns(new TfdWhatsAppContexto("phone-1", "https://zap.exemplo", "token-de-teste", null));
        var negados = Substitute.For<IContatoNegadoService>();
        negados.BloqueadoAsync(default, default, default).ReturnsForAnyArgs(false);
        return new WhatsAppCliente(
            new HttpClient(relay), config, db, new ConfigurationBuilder().Build(),
            cache ?? new MemoryCache(new MemoryCacheOptions()), negados,
            Options.Create(new ComunicacaoPacienteOptions()), NullLogger<WhatsAppCliente>.Instance);
    }

    /// <summary>O Automais.Zap de mentira: entrega o catálogo e guarda o que foi pedido para envio.</summary>
    private sealed class RelayFalso(string catalogo) : HttpMessageHandler
    {
        public bool CatalogoForaDoAr { get; set; }
        public List<string> Envios { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/v1/templates"))
                return CatalogoForaDoAr
                    ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(catalogo, Encoding.UTF8, "application/json"),
                    };

            Envios.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"messages":[{"id":"wamid.TESTE"}]}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
