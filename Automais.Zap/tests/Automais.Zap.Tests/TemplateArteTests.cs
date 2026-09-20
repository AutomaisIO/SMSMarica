using Automais.Zap.Core.Midias;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;

namespace Automais.Zap.Tests;

/// <summary>
/// A arte escolhida para um modelo. É isto que o catálogo entrega pronto às instâncias — sem
/// ele, cada município teria de manter o próprio mapa "modelo → imagem", e a plataforma não
/// saberia dizer qual arte está no ar.
/// </summary>
[Collection(nameof(PostgresZapCollection))]
public sealed class TemplateArteTests(PostgresZapFixture fixture)
{
    private static byte[] Png(int largura)
    {
        var b = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0, 0, 0, 0, 0, 0, 0, 0,
            0x08, 0x06, 0x00, 0x00, 0x00,
        };
        BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(largura)).CopyTo(b, 16);
        BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(628)).CopyTo(b, 20);
        return b;
    }

    private sealed record Cenario(Tenant Tenant, Waba Waba);

    private static async Task<Cenario> SemearAsync(ZapDbContext db)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..12];
        var tenant = new Tenant { Nome = "Cliente " + sufixo, CriadoEm = DateTimeOffset.UtcNow };
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
        return new Cenario(tenant, waba);
    }

    private static (MidiaService Midias, TemplateArteService Artes) Montar(ZapDbContext db)
        => (new MidiaService(db, TimeProvider.System), new TemplateArteService(db, TimeProvider.System));

    [Fact]
    public async Task Escolher_a_arte_faz_o_modelo_apontar_para_ela()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var (midias, artes) = Montar(db);

        var (midia, _) = await midias.GuardarAsync(
            c.Tenant.Id, "arte.png", "image/png", Png(1200), "cabecalho", null);

        var (ok, erro) = await artes.DefinirAsync(c.Waba.Id, "confirmacao_exame", midia!.Id, null);

        ok.Should().BeTrue();
        erro.Should().BeNull();

        var mapa = await artes.MapaAsync(c.Waba.Id);
        mapa.Should().ContainKey("confirmacao_exame");
        mapa["confirmacao_exame"].Caminho.Should().Be($"/midias/{midia.Id}");
    }

    [Fact]
    public async Task Trocar_a_arte_substitui_em_vez_de_acumular()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var (midias, artes) = Montar(db);

        var (primeira, _) = await midias.GuardarAsync(c.Tenant.Id, "a.png", "image/png", Png(1200), "cabecalho", null);
        var (segunda, _) = await midias.GuardarAsync(c.Tenant.Id, "b.png", "image/png", Png(1201), "cabecalho", null);

        await artes.DefinirAsync(c.Waba.Id, "confirmacao_exame", primeira!.Id, null);
        await artes.DefinirAsync(c.Waba.Id, "confirmacao_exame", segunda!.Id, null);

        var mapa = await artes.MapaAsync(c.Waba.Id);
        mapa.Should().ContainSingle("um modelo tem UMA arte");
        mapa["confirmacao_exame"].MidiaId.Should().Be(segunda.Id);
    }

    [Fact]
    public async Task Arte_de_outro_cliente_nao_entra_no_canal_daqui()
    {
        await using var db = fixture.CriarContexto();
        var meu = await SemearAsync(db);
        var alheio = await SemearAsync(db);
        var (midias, artes) = Montar(db);

        var (daOutra, _) = await midias.GuardarAsync(
            alheio.Tenant.Id, "arte.png", "image/png", Png(1200), "cabecalho", null);

        var (ok, erro) = await artes.DefinirAsync(meu.Waba.Id, "confirmacao_exame", daOutra!.Id, null);

        ok.Should().BeFalse("um id vazado não pode publicar a arte de um município no canal de outro");
        erro.Should().Contain("não encontrada");
    }

    [Fact]
    public async Task Tirar_a_arte_deixa_o_modelo_sem_nenhuma()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var (midias, artes) = Montar(db);

        var (midia, _) = await midias.GuardarAsync(c.Tenant.Id, "arte.png", "image/png", Png(1200), "cabecalho", null);
        await artes.DefinirAsync(c.Waba.Id, "confirmacao_exame", midia!.Id, null);

        (await artes.RemoverAsync(c.Waba.Id, "confirmacao_exame")).Should().BeTrue();
        (await artes.MapaAsync(c.Waba.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_plataforma_sabe_quais_modelos_usam_uma_arte()
    {
        await using var db = fixture.CriarContexto();
        var c = await SemearAsync(db);
        var (midias, artes) = Montar(db);

        var (midia, _) = await midias.GuardarAsync(c.Tenant.Id, "arte.png", "image/png", Png(1200), "cabecalho", null);
        await artes.DefinirAsync(c.Waba.Id, "confirmacao_exame", midia!.Id, null);
        await artes.DefinirAsync(c.Waba.Id, "confirmacao_consulta", midia.Id, null);

        // É o que impede o painel de apagar uma arte e derrubar o envio de dois modelos em silêncio.
        var emUso = await artes.EmUsoPorAsync(midia.Id);
        emUso.Should().BeEquivalentTo(["confirmacao_consulta", "confirmacao_exame"]);
    }
}
