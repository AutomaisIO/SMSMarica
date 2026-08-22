using System.Text;
using System.Text.Json;
using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Relay;
using Automais.Zap.Core.Roteamento;
using Automais.Zap.Core.Seguranca;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Automais.Zap.Tests;

[Collection(nameof(PostgresZapCollection))]
public sealed class RelayServiceTests(PostgresZapFixture fixture)
{
    private const string AppSecret = "app-secret-de-teste";

    /// <summary>Entregador de mentira: guarda o que recebeu e obedece ao veredito combinado.</summary>
    private sealed class EntregadorFalso(bool sucesso = true) : IEntregador
    {
        public List<(string Url, byte[] Corpo, string Assinatura, string? Segredo)> Chamadas { get; } = [];

        public Task<ResultadoEntrega> EntregarAsync(
            string url, byte[] corpo, string assinatura, string? segredoProprio = null, CancellationToken ct = default)
        {
            Chamadas.Add((url, corpo, assinatura, segredoProprio));
            return Task.FromResult(sucesso
                ? new ResultadoEntrega(true, 200, 5, null)
                : new ResultadoEntrega(false, 500, 5, "HTTP 500"));
        }
    }

    /// <summary>Credenciais fixas: o teste é do roteamento, não da configuração.</summary>
    private sealed class ConfiguracaoMetaFalsa(string? appSecret) : IConfiguracaoMetaService
    {
        public Task<CredenciaisMeta> ObterAsync(CancellationToken ct = default)
            => Task.FromResult(new CredenciaisMeta("app", appSecret, "vt", null, "https://graph.facebook.com/v21.0/"));

        public Task<ConfiguracaoMeta> ObterBrutaAsync(CancellationToken ct = default)
            => Task.FromResult(new ConfiguracaoMeta());

        public Task SalvarAsync(AtualizarConfiguracaoMeta dados, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    /// <summary>Sem anel de Data Protection no teste: o segredo vai e volta como esta.</summary>
    private sealed class ProtetorSegredosFalso : IProtetorSegredos
    {
        public string Proteger(string textoPuro) => textoPuro;
        public string? Revelar(string? textoCifrado) => string.IsNullOrWhiteSpace(textoCifrado) ? null : textoCifrado;
    }

    private static RelayService Montar(ZapDbContext db, IEntregador entregador, string? appSecret = AppSecret)
        => new(
            db,
            new Roteador(db, new ProtetorSegredosFalso()),
            entregador,
            new ConfiguracaoMetaFalsa(appSecret),
            TimeProvider.System,
            NullLogger<RelayService>.Instance);

    private sealed record Semeado(Tenant Tenant, Waba Waba, string PhoneNumberId, string WabaIdMeta, string Url);

    private static async Task<Semeado> SemearAsync(
        ZapDbContext db,
        string url,
        bool tenantAtivo = true,
        bool tenantSuspenso = false,
        bool roteamentoAtivo = true,
        bool numeroAtivo = true)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];

        var tenant = new Tenant
        {
            Nome = "Cliente " + sufixo,
            Ativo = tenantAtivo,
            SuspensoEm = tenantSuspenso ? DateTimeOffset.UtcNow : null,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        var waba = new Waba
        {
            WabaId = "WABA_" + sufixo,
            Nome = "WABA " + sufixo,
            UrlDestino = url,
            RoteamentoAtivo = roteamentoAtivo,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        var numero = new Numero
        {
            PhoneNumberId = "NUM_" + sufixo,
            Ativo = numeroAtivo,
            CriadoEm = DateTimeOffset.UtcNow,
        };

        // Grava em tres passos e amarra a FK na mao: depender de fixup de navegacao aqui
        // deixa a ordem de insercao a cargo do EF, e a FK de numero->waba estoura.
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        waba.TenantId = tenant.Id;
        db.Wabas.Add(waba);
        await db.SaveChangesAsync();

        numero.WabaId = waba.Id;
        db.Numeros.Add(numero);
        await db.SaveChangesAsync();

        return new Semeado(tenant, waba, numero.PhoneNumberId, waba.WabaId, url);
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
        var s = await SemearAsync(db, "https://destino.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var resultado = await relay.ProcessarAsync(PayloadDe((s.WabaIdMeta, s.PhoneNumberId)), "sha256=deadbeef");

        resultado.Situacao.Should().Be(SituacaoRelay.AssinaturaInvalida);
        entregador.Chamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Dono_unico_recebe_os_bytes_originais_e_a_assinatura_original()
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://dono-unico.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((s.WabaIdMeta, s.PhoneNumberId));
        var assinatura = AssinaturaMeta.Calcular(AppSecret, corpo);

        var resultado = await relay.ProcessarAsync(corpo, assinatura);

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        resultado.Entregues.Should().Be(1);

        var chamada = entregador.Chamadas.Should().ContainSingle().Subject;
        chamada.Url.Should().Be(s.Url);
        // Byte a byte: é o que permite a instância validar sem mudar uma linha de código.
        chamada.Corpo.Should().Equal(corpo);
        chamada.Assinatura.Should().Be(assinatura);
    }

    [Fact]
    public async Task Lote_com_dois_donos_recorta_e_cada_um_so_ve_o_seu()
    {
        await using var db = fixture.CriarContexto();
        var a = await SemearAsync(db, "https://a.exemplo/webhook");
        var b = await SemearAsync(db, "https://b.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((a.WabaIdMeta, a.PhoneNumberId), (b.WabaIdMeta, b.PhoneNumberId));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        resultado.Entregues.Should().Be(2);
        entregador.Chamadas.Should().HaveCount(2);

        var paraA = entregador.Chamadas.Single(c => c.Url == a.Url);
        var paraB = entregador.Chamadas.Single(c => c.Url == b.Url);

        Encoding.UTF8.GetString(paraA.Corpo).Should().Contain(a.PhoneNumberId).And.NotContain(b.PhoneNumberId);
        Encoding.UTF8.GetString(paraB.Corpo).Should().Contain(b.PhoneNumberId).And.NotContain(a.PhoneNumberId);
    }

    [Fact]
    public async Task Recorte_vai_assinado_com_o_mesmo_app_secret_e_a_instancia_valida()
    {
        await using var db = fixture.CriarContexto();
        var a = await SemearAsync(db, "https://a2.exemplo/webhook");
        var b = await SemearAsync(db, "https://b2.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((a.WabaIdMeta, a.PhoneNumberId), (b.WabaIdMeta, b.PhoneNumberId));
        await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        var paraA = entregador.Chamadas.Single(c => c.Url == a.Url);

        // É esta linha que garante "zero mudança no monolito": o destino confere o HMAC do
        // corpo recortado com o App Secret que ele já tem, e passa.
        AssinaturaMeta.Confere(AppSecret, paraA.Corpo, paraA.Assinatura).Should().BeTrue();

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

        var log = await db.EntregasLog.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumberId == orfao);
        log.Should().NotBeNull();
        log!.Erro.Should().Be("sem rota cadastrada");
        log.TenantId.Should().BeNull();
    }

    [Theory]
    [InlineData(false, false, true, true, "tenant inativo")]
    [InlineData(true, true, true, true, "tenant suspenso")]
    [InlineData(true, false, false, true, "roteamento do WABA desligado")]
    [InlineData(true, false, true, false, "numero desligado")]
    public async Task Qualquer_chave_desligada_para_a_entrega(
        bool tenantAtivo, bool tenantSuspenso, bool roteamentoAtivo, bool numeroAtivo, string caso)
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://off.exemplo/webhook",
            tenantAtivo: tenantAtivo, tenantSuspenso: tenantSuspenso,
            roteamentoAtivo: roteamentoAtivo, numeroAtivo: numeroAtivo);

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((s.WabaIdMeta, s.PhoneNumberId));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.SemRota.Should().Be(1, because: caso);
        entregador.Chamadas.Should().BeEmpty(because: caso);
    }

    [Fact]
    public async Task Override_do_numero_ganha_do_destino_do_waba()
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://waba.exemplo/webhook");

        var numero = await db.Numeros.FirstAsync(n => n.PhoneNumberId == s.PhoneNumberId);
        numero.UrlDestinoOverride = "https://excecao.exemplo/webhook";
        await db.SaveChangesAsync();

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((s.WabaIdMeta, s.PhoneNumberId));
        await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        entregador.Chamadas.Should().ContainSingle().Which.Url.Should().Be("https://excecao.exemplo/webhook");
    }

    [Fact]
    public async Task Destino_fora_do_ar_devolve_falha_para_a_Meta_reentregar()
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://caiu.exemplo/webhook");

        var entregador = new EntregadorFalso(sucesso: false);
        var relay = Montar(db, entregador);

        var corpo = PayloadDe((s.WabaIdMeta, s.PhoneNumberId));
        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.FalhaDeEntrega);
        resultado.Falhas.Should().Be(1);
    }

    [Fact]
    public async Task Evento_sem_numero_cai_no_dono_do_waba()
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://waba-fallback.exemplo/webhook");

        var entregador = new EntregadorFalso();
        var relay = Montar(db, entregador);

        var corpo = Encoding.UTF8.GetBytes($$"""
        {"object":"whatsapp_business_account","entry":[
          {"id":"{{s.WabaIdMeta}}","changes":[{"field":"message_template_status_update","value":{"event":"APPROVED"} }]}
        ]}
        """);

        var resultado = await relay.ProcessarAsync(corpo, AssinaturaMeta.Calcular(AppSecret, corpo));

        resultado.Situacao.Should().Be(SituacaoRelay.Ok);
        entregador.Chamadas.Should().ContainSingle().Which.Url.Should().Be(s.Url);
    }

    [Fact]
    public async Task Phone_number_id_e_unico_no_relay_inteiro()
    {
        await using var db = fixture.CriarContexto();
        var s = await SemearAsync(db, "https://dono1.exemplo/webhook");
        var outro = await SemearAsync(db, "https://dono2.exemplo/webhook");

        // O banco tem de recusar: dois tenants com o mesmo número seria mensagem de um
        // município caindo em outro.
        db.Numeros.Add(new Numero
        {
            WabaId = outro.Waba.Id,
            PhoneNumberId = s.PhoneNumberId,
            CriadoEm = DateTimeOffset.UtcNow,
        });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
