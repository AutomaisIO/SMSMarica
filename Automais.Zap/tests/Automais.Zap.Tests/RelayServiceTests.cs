using System.Text;
using System.Text.Json;
using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Automais.Zap.Core.Roteamento;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Tests;

[Collection(nameof(PostgresZapCollection))]
public sealed class RelayServiceTests(PostgresZapFixture fixture)
{
    private const string AppSecret = "app-secret-de-teste";

    /// <summary>Entregador de mentira: guarda o que recebeu e obedece ao veredito combinado.</summary>
    private sealed class EntregadorFalso(bool sucesso = true) : IEntregador
    {
        public List<(string Url, byte[] Corpo, string Assinatura)> Chamadas { get; } = [];

        public Task<ResultadoEntrega> EntregarAsync(
            string url, byte[] corpo, string assinatura, CancellationToken ct = default)
        {
            Chamadas.Add((url, corpo, assinatura));
            return Task.FromResult(sucesso
                ? new ResultadoEntrega(true, 200, 5, null)
                : new ResultadoEntrega(false, 500, 5, "HTTP 500"));
        }
    }

    private static RelayService Montar(ZapDbContext db, IEntregador entregador, string? appSecret = AppSecret)
        => new(
            db,
            new Roteador(db),
            entregador,
            Options.Create(new MetaOptions { AppSecret = appSecret, VerifyToken = "vt" }),
            TimeProvider.System,
            NullLogger<RelayService>.Instance);

    private static async Task<(Destino Destino, string PhoneNumberId)> SemearAsync(
        ZapDbContext db, string url, bool destinoAtivo = true, bool numeroAtivo = true, string? waba = null)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];
        var destino = new Destino
        {
            Nome = "Cliente " + sufixo,
            UrlWebhook = url,
            Ativo = destinoAtivo,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        var numero = new Numero
        {
            Destino = destino,
            PhoneNumberId = "NUM_" + sufixo,
            WabaId = waba,
            Ativo = numeroAtivo,
            CriadoEm = DateTimeOffset.UtcNow,
        };

        db.Destinos.Add(destino);
        db.Numeros.Add(numero);
        await db.SaveChangesAsync();

        return (destino, numero.PhoneNumberId);
    }

    private static byte[] PayloadDe(params (string Waba, string PhoneNumberId)[] donos)
    {
        var entries = donos.Select(d => $$"""
            {
              "id": "{{d.Waba}}",
              "changes": [{
                "field": "messages",
                "value": {
                  "messaging_product": "whatsapp",
                  "metadata": { "phone_number_id": "{{d.PhoneNumberId}}" },
                  "messages": [{ "id": "wamid.{{d.PhoneNumberId}}", "type": "text" }]
                }
              }]
            }
            """);

        return Encoding.UTF8.GetBytes(
            $$"""{"object":"whatsapp_business_account","entry":[{{string.Join(",", entries)}}]}""");
    }

    [Fact]
    public async Task Sem_app_secret_falha_fechado_e_nao_processa()
    {
        await using var db = fixture.CriarContexto();
        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador, appSecret: null);

        var corpo = PayloadDe(("W", "QUALQUER"));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.NaoConfigurado);
        entregador.Chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Assinatura_invalida_nao_entrega_nada()
    {
        await using var db = fixture.CriarContexto();
        var (_, numero) = await SemearAsync(db, "https://destino.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var resultado = await relay.ProcessarAsync(PayloadDe(("W", numero)), "sha256=deadbeef");

        resultado.Situacao.Should().Be(SituacaoRelay.AssinaturaInvalida);
        entregador.Chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Dono_unico_recebe_os_bytes_originais_e_a_assinatura_original()
    {
        await using var db = fixture.CriarContexto();
        var (destino, numero) = await SemearAsync(db, "https://marica.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("W", numero));
        var assinatura = AssinaturaMeta.Calcular(AppSecret, corpo);

        var resultado = await relay.ProcessarAsync(corpo, assinatura);

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        resultado.Entregues.Should().Be(1);

        var chamada = entregador.Chamadas.Should().ContainSingle().Subject;
        chamada.Url.Should().Be(destino.UrlWebhook);
        // Byte a byte: é o que permite a instância validar sem mudar uma linha de código.
        chamada.Corpo.Should().Equal(corpo);
        chamada.Assinatura.Should().Be(assinatura);
    }

    [Fact]
    public async Task Lote_com_dois_donos_recorta_e_cada_um_so_ve_o_seu()
    {
        await using var db = fixture.CriarContexto();
        var (destinoA, numeroA) = await SemearAsync(db, "https://a.exemplo/webhook");
        var (destinoB, numeroB) = await SemearAsync(db, "https://b.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("WABA_A", numeroA), ("WABA_B", numeroB));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        resultado.Entregues.Should().Be(2);
        entregador.Chamadas.Should().HaveCount(2);

        var paraA = entregador.Chamadas.Single(c => c.Url == destinoA.UrlWebhook);
        var paraB = entregador.Chamadas.Single(c => c.Url == destinoB.UrlWebhook);

        var textoA = Encoding.UTF8.GetString(paraA.Corpo);
        textoA.Should().Contain(numeroA);
        textoA.Should().NotContain(numeroB);

        var textoB = Encoding.UTF8.GetString(paraB.Corpo);
        textoB.Should().Contain(numeroB);
        textoB.Should().NotContain(numeroA);
    }

    [Fact]
    public async Task Recorte_vai_assinado_com_o_mesmo_app_secret_e_a_instancia_valida()
    {
        await using var db = fixture.CriarContexto();
        var (destinoA, numeroA) = await SemearAsync(db, "https://a2.exemplo/webhook");
        var (_, numeroB) = await SemearAsync(db, "https://b2.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("WABA_A", numeroA), ("WABA_B", numeroB));
        await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        var paraA = entregador.Chamadas.Single(c => c.Url == destinoA.UrlWebhook);

        // É esta linha que garante "zero mudança no monolito": o destino confere o HMAC do
        // corpo recortado com o App Secret que ele já tem, e passa.
        AssinaturaMeta.Confere(AppSecret, paraA.Corpo, paraA.Assinatura).Should().BeTrue();

        // E o recorte continua sendo um payload legível.
        using var doc = JsonDocument.Parse(paraA.Corpo);
        doc.RootElement.GetProperty("entry").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Numero_desconhecido_nao_entrega_e_registra_sem_rota()
    {
        await using var db = fixture.CriarContexto();
        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var orfao = "NUM_ORFAO_" + Guid.NewGuid().ToString("N")[..8];
        var corpo = PayloadDe(("W", orfao));

        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        // 200 de propósito: reentregar não faria o número passar a existir.
        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        resultado.SemRota.Should().Be(1);
        resultado.Entregues.Should().Be(0);
        entregador.Chamadas.Should().BeEmpty();

        var log = await db.EntregasLog.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PhoneNumberId == orfao);
        log.Should().NotBeNull();
        log!.Erro.Should().Be("sem rota cadastrada");
        log.DestinoId.Should().BeNull();
    }

    [Fact]
    public async Task Destino_suspenso_para_de_receber()
    {
        await using var db = fixture.CriarContexto();
        var (_, numero) = await SemearAsync(db, "https://suspenso.exemplo/webhook", destinoAtivo: false);

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("W", numero));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.SemRota.Should().Be(1);
        entregador.Chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Numero_desligado_para_de_receber()
    {
        await using var db = fixture.CriarContexto();
        var (_, numero) = await SemearAsync(db, "https://numoff.exemplo/webhook", numeroAtivo: false);

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("W", numero));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.SemRota.Should().Be(1);
        entregador.Chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Destino_fora_do_ar_devolve_falha_para_a_Meta_reentregar()
    {
        await using var db = fixture.CriarContexto();
        var (_, numero) = await SemearAsync(db, "https://caiu.exemplo/webhook");

        var entregador = new EntregadorFalso(sucesso: false);
        var relay = Montar(db, entregador);

        var corpo = PayloadDe(("W", numero));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.FalhaDeEntrega);
        resultado.Falhas.Should().Be(1);
    }

    [Fact]
    public async Task Evento_sem_numero_cai_no_dono_do_waba()
    {
        await using var db = fixture.CriarContexto();
        var waba = "WABA_" + Guid.NewGuid().ToString("N")[..8];
        var (destino, _) = await SemearAsync(db, "https://waba.exemplo/webhook", waba: waba);

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = Encoding.UTF8.GetBytes($$"""
        {"object":"whatsapp_business_account","entry":[
          {"id":"{{waba}}","changes":[{"field":"message_template_status_update","value":{"event":"APPROVED"} }]}
        ]}
        """);

        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        entregador.Chamadas.Should().ContainSingle().Which.Url.Should().Be(destino.UrlWebhook);
    }

    [Fact]
    public async Task Phone_number_id_e_unico_no_relay_inteiro()
    {
        await using var db = fixture.CriarContexto();
        var (_, numero) = await SemearAsync(db, "https://dono1.exemplo/webhook");

        var outro = new Destino
        {
            Nome = "Outro " + Guid.NewGuid().ToString("N")[..8],
            UrlWebhook = "https://dono2.exemplo/webhook",
            CriadoEm = DateTimeOffset.UtcNow,
        };
        db.Destinos.Add(outro);
        db.Numeros.Add(new Numero { Destino = outro, PhoneNumberId = numero, CriadoEm = DateTimeOffset.UtcNow });

        // O banco tem de recusar: dois destinos com o mesmo número seria mensagem de um
        // município caindo em outro.
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
