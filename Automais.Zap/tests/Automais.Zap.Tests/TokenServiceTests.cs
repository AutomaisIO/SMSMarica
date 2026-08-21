using Automais.Zap.Core.Tokens;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Automais.Zap.Tests;

[Collection(nameof(PostgresZapCollection))]
public sealed class TokenServiceTests(PostgresZapFixture fixture)
{
    private static TokenService Montar(ZapDbContext db)
        => new(db, TimeProvider.System, NullLogger<TokenService>.Instance);

    private sealed record Cenario(Tenant Tenant, Waba Waba, Numero A, Numero B);

    private static async Task<Cenario> SemearAsync(ZapDbContext db, bool tenantSuspenso = false)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];

        var tenant = new Tenant
        {
            Nome = "Cliente " + sufixo,
            SuspensoEm = tenantSuspenso ? DateTimeOffset.UtcNow : null,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var waba = new Waba
        {
            TenantId = tenant.Id,
            WabaId = "WABA_" + sufixo,
            UrlDestino = "https://x.exemplo/webhook",
            RoteamentoAtivo = true,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        db.Wabas.Add(waba);
        await db.SaveChangesAsync();

        var a = new Numero { WabaId = waba.Id, PhoneNumberId = "NUM_A_" + sufixo, CriadoEm = DateTimeOffset.UtcNow };
        var b = new Numero { WabaId = waba.Id, PhoneNumberId = "NUM_B_" + sufixo, CriadoEm = DateTimeOffset.UtcNow };
        db.Numeros.AddRange(a, b);
        await db.SaveChangesAsync();

        return new Cenario(tenant, waba, a, b);
    }

    [Fact]
    public async Task Token_criado_vem_em_claro_uma_vez_e_o_banco_guarda_so_o_resumo()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);

        var criado = await svc.CriarAsync(c.Tenant.Id, "Sistema do cliente", true, []);

        criado.ValorEmClaro.Should().StartWith("zap_");
        criado.ValorEmClaro.Split('_').Should().HaveCount(3);

        var doBanco = await db.TenantTokens.AsNoTracking().FirstAsync(t => t.Id == criado.Registro.Id);
        // O que fica guardado nao pode reconstruir o token.
        doBanco.Hash.Should().NotContain(criado.ValorEmClaro);
        doBanco.Hash.Should().MatchRegex("^[0-9a-f]{64}$");
        doBanco.Prefixo.Should().NotBe(criado.ValorEmClaro);
    }

    [Fact]
    public async Task Autentica_com_o_token_certo_e_recusa_o_errado()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "t", true, []);

        (await svc.AutenticarAsync("Bearer " + criado.ValorEmClaro))!.TenantId.Should().Be(c.Tenant.Id);
        (await svc.AutenticarAsync(criado.ValorEmClaro))!.TenantId.Should().Be(c.Tenant.Id);

        var adulterado = criado.ValorEmClaro[..^4] + "0000";
        (await svc.AutenticarAsync("Bearer " + adulterado)).Should().BeNull();
        (await svc.AutenticarAsync("Bearer zap_naoexiste_000")).Should().BeNull();
        (await svc.AutenticarAsync("lixo")).Should().BeNull();
        (await svc.AutenticarAsync(null)).Should().BeNull();
    }

    [Fact]
    public async Task Token_revogado_para_de_autenticar()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "t", true, []);

        (await svc.AutenticarAsync(criado.ValorEmClaro)).Should().NotBeNull();
        await svc.RevogarAsync(criado.Registro.Id);
        (await svc.AutenticarAsync(criado.ValorEmClaro)).Should().BeNull();
    }

    [Fact]
    public async Task Tenant_suspenso_derruba_o_token_junto()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db, tenantSuspenso: true);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "t", true, []);

        // A suspensao vale para os dois sentidos, nao so para o recebimento.
        (await svc.AutenticarAsync(criado.ValorEmClaro)).Should().BeNull();
    }

    [Fact]
    public async Task Token_com_alcance_total_envia_por_qualquer_numero_do_tenant()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "t", todosNumeros: true, []);
        var chamador = (await svc.AutenticarAsync(criado.ValorEmClaro))!;

        (await svc.AutorizarNumeroAsync(chamador, c.A.PhoneNumberId)).Should().NotBeNull();
        (await svc.AutorizarNumeroAsync(chamador, c.B.PhoneNumberId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Token_recortado_so_envia_pelos_numeros_liberados()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "so o A", todosNumeros: false, [c.A.Id]);
        var chamador = (await svc.AutenticarAsync(criado.ValorEmClaro))!;

        (await svc.AutorizarNumeroAsync(chamador, c.A.PhoneNumberId)).Should().NotBeNull();
        (await svc.AutorizarNumeroAsync(chamador, c.B.PhoneNumberId)).Should().BeNull();
    }

    [Fact]
    public async Task Token_de_um_tenant_nao_envia_pelo_numero_de_outro()
    {
        await using var db = fixture.CriarContexto();
        var meu = await SemearAsync(db);
        var alheio = await SemearAsync(db);
        var svc = Montar(db);

        var criado = await svc.CriarAsync(meu.Tenant.Id, "t", todosNumeros: true, []);
        var chamador = (await svc.AutenticarAsync(criado.ValorEmClaro))!;

        // O phone_number_id nao e segredo: sem esta checagem, saber o numero do vizinho
        // bastaria para enviar em nome dele.
        (await svc.AutorizarNumeroAsync(chamador, alheio.A.PhoneNumberId)).Should().BeNull();
    }

    [Fact]
    public async Task Numero_desligado_nao_aceita_envio()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var svc = Montar(db);
        var criado = await svc.CriarAsync(c.Tenant.Id, "t", true, []);
        var chamador = (await svc.AutenticarAsync(criado.ValorEmClaro))!;

        var numero = await db.Numeros.FirstAsync(n => n.Id == c.A.Id);
        numero.Ativo = false;
        await db.SaveChangesAsync();

        (await svc.AutorizarNumeroAsync(chamador, c.A.PhoneNumberId)).Should().BeNull();
    }
}
