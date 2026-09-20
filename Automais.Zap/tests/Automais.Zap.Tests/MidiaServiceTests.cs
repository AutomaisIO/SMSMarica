using Automais.Zap.Core.Midias;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;

namespace Automais.Zap.Tests;

/// <summary>
/// Arquivos que a plataforma hospeda para a Meta baixar — a arte do cabeçalho dos modelos com
/// foto no topo.
///
/// <para>O que se testa: o arquivo volta com URL utilizável, o mesmo arquivo não vira endereço
/// novo a cada upload, e o que a Meta não aceita é recusado AQUI — não na hora do envio, quando
/// já custou uma mensagem.</para>
/// </summary>
[Collection(nameof(PostgresZapCollection))]
public sealed class MidiaServiceTests(PostgresZapFixture fixture)
{
    /// <summary>PNG 1×1 de verdade: o cabeçalho traz as dimensões, que o serviço lê.</summary>
    private static byte[] PngDe(int largura, int altura)
    {
        var b = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,       // assinatura PNG
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,       // tamanho + "IHDR"
            0, 0, 0, 0, 0, 0, 0, 0,                                // largura e altura (abaixo)
            0x08, 0x06, 0x00, 0x00, 0x00,
        };
        BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(largura)).CopyTo(b, 16);
        BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(altura)).CopyTo(b, 20);
        return b;
    }

    private static async Task<Tenant> SemearTenantAsync(ZapDbContext db)
    {
        var tenant = new Tenant
        {
            Nome = "Cliente " + Guid.NewGuid().ToString("N")[..8],
            CriadoEm = DateTimeOffset.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static MidiaService Montar(ZapDbContext db) => new(db, TimeProvider.System);

    [Fact]
    public async Task Guarda_a_arte_e_devolve_um_caminho_utilizavel()
    {
        await using var db = fixture.CriarContexto();
        var tenant = await SemearTenantAsync(db);

        var (midia, erro) = await Montar(db).GuardarAsync(
            tenant.Id, "arte.png", "image/png", PngDe(1200, 628), "cabecalho", null);

        erro.Should().BeNull();
        midia.Should().NotBeNull();
        midia!.Caminho.Should().Be($"/midias/{midia.Id}");
        midia.MimeType.Should().Be("image/png");

        // A tela avisa sobre proporção; para isso precisa das dimensões.
        midia.Largura.Should().Be(1200);
        midia.Altura.Should().Be(628);
    }

    [Fact]
    public async Task Mesmo_arquivo_reenviado_mantem_a_mesma_url()
    {
        await using var db = fixture.CriarContexto();
        var tenant = await SemearTenantAsync(db);
        var servico = Montar(db);
        var bytes = PngDe(800, 419);

        var (primeira, _) = await servico.GuardarAsync(tenant.Id, "a.png", "image/png", bytes, "cabecalho", null);
        var (segunda, _) = await servico.GuardarAsync(tenant.Id, "outro-nome.png", "image/png", bytes, "cabecalho", null);

        // Reenviar a mesma arte não pode trocar o endereço: modelo apontado para o antigo
        // continuaria válido, mas o banco acumularia cópia a cada tentativa.
        segunda!.Id.Should().Be(primeira!.Id);
    }

    [Fact]
    public async Task Dois_tenants_com_a_mesma_arte_nao_compartilham_registro()
    {
        await using var db = fixture.CriarContexto();
        var a = await SemearTenantAsync(db);
        var b = await SemearTenantAsync(db);
        var servico = Montar(db);
        var bytes = PngDe(600, 314);

        var (daA, _) = await servico.GuardarAsync(a.Id, "arte.png", "image/png", bytes, "cabecalho", null);
        var (daB, _) = await servico.GuardarAsync(b.Id, "arte.png", "image/png", bytes, "cabecalho", null);

        daB!.Id.Should().NotBe(daA!.Id, "a arte de um município não pode virar a do outro por coincidência de bytes");
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    public async Task Tipo_que_a_meta_nao_aceita_e_recusado_na_hora(string mime)
    {
        await using var db = fixture.CriarContexto();
        var tenant = await SemearTenantAsync(db);

        var (midia, erro) = await Montar(db).GuardarAsync(
            tenant.Id, "arte.bin", mime, [1, 2, 3, 4], "cabecalho", null);

        midia.Should().BeNull();
        erro.Should().Contain("PNG ou JPEG");
    }

    [Fact]
    public async Task Extensao_desempata_quando_o_navegador_manda_o_tipo_errado()
    {
        await using var db = fixture.CriarContexto();
        var tenant = await SemearTenantAsync(db);

        // Upload com application/octet-stream acontece; recusar um PNG legítimo por causa disso
        // daria um erro que o operador não tem como entender.
        var (midia, erro) = await Montar(db).GuardarAsync(
            tenant.Id, "arte.PNG", "application/octet-stream", PngDe(400, 209), "cabecalho", null);

        erro.Should().BeNull();
        midia!.MimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task Arquivo_acima_do_teto_da_meta_e_recusado()
    {
        await using var db = fixture.CriarContexto();
        var tenant = await SemearTenantAsync(db);

        var grande = new byte[MidiaService.TamanhoMaximoBytes + 1];
        var (midia, erro) = await Montar(db).GuardarAsync(
            tenant.Id, "grande.png", "image/png", grande, "cabecalho", null);

        midia.Should().BeNull();
        erro.Should().Contain("5 MB");
    }

    [Fact]
    public async Task So_apaga_a_arte_do_proprio_tenant()
    {
        await using var db = fixture.CriarContexto();
        var dono = await SemearTenantAsync(db);
        var outro = await SemearTenantAsync(db);
        var servico = Montar(db);

        var (midia, _) = await servico.GuardarAsync(
            dono.Id, "arte.png", "image/png", PngDe(300, 157), "cabecalho", null);

        (await servico.ApagarAsync(outro.Id, midia!.Id)).Should().BeFalse();
        (await servico.ObterConteudoAsync(midia.Id)).Should().NotBeNull("continua no ar para quem é dono");

        (await servico.ApagarAsync(dono.Id, midia.Id)).Should().BeTrue();
        (await servico.ObterConteudoAsync(midia.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A_listagem_e_recortada_por_tenant()
    {
        await using var db = fixture.CriarContexto();
        var a = await SemearTenantAsync(db);
        var b = await SemearTenantAsync(db);
        var servico = Montar(db);

        await servico.GuardarAsync(a.Id, "a.png", "image/png", PngDe(100, 52), "cabecalho", null);
        await servico.GuardarAsync(b.Id, "b.png", "image/png", PngDe(101, 53), "cabecalho", null);

        var deA = await servico.ListarAsync(a.Id, "cabecalho");

        deA.Should().ContainSingle();
        deA[0].NomeArquivo.Should().Be("a.png");
    }
}
