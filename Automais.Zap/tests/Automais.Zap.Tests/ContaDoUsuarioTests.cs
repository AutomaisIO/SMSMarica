using Automais.Zap.Core.Admin;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Automais.Zap.Tests.Infraestrutura;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Tests;

/// <summary>
/// A conta do próprio operador: trocar a senha e o nome de exibição.
///
/// <para>O que se testa aqui é a guarda: trocar senha exige a senha em vigor. Sem isso, uma
/// sessão esquecida aberta num computador da recepção vira tomada de conta — e quem recebeu
/// senha inicial da casa continuaria com ela para sempre, que era a situação antes.</para>
/// </summary>
[Collection(nameof(PostgresZapCollection))]
public sealed class ContaDoUsuarioTests(PostgresZapFixture fixture)
{
    private const string SenhaInicial = "senha-inicial-123";

    private static AdminService Montar(ZapDbContext db)
        => new(db, Options.Create(new AdminOptions()), TimeProvider.System, NullLogger<AdminService>.Instance);

    private static async Task<UsuarioAdmin> SemearAsync(ZapDbContext db, bool ativo = true)
    {
        var usuario = new UsuarioAdmin
        {
            Email = $"operador-{Guid.NewGuid():N}@exemplo.test",
            Nome = "Operador de Teste",
            SenhaHash = Senhas.Gerar(SenhaInicial),
            Global = false,
            Ativo = ativo,
            CriadoEm = DateTimeOffset.UtcNow,
        };
        db.UsuariosAdmin.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task Troca_a_senha_quando_a_atual_confere()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);
        var admin = Montar(db);

        var (ok, erro) = await admin.TrocarSenhaAsync(usuario.Id, SenhaInicial, "outra-senha-bem-maior");

        ok.Should().BeTrue();
        erro.Should().BeNull();

        // A prova é entrar com a nova e não conseguir com a antiga.
        (await admin.AutenticarAsync(usuario.Email, "outra-senha-bem-maior")).Should().NotBeNull();
        (await admin.AutenticarAsync(usuario.Email, SenhaInicial)).Should().BeNull();
    }

    [Fact]
    public async Task Senha_atual_errada_nao_troca_nada()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);
        var admin = Montar(db);

        var (ok, erro) = await admin.TrocarSenhaAsync(usuario.Id, "chute-errado-mas-longo", "nova-senha-legitima");

        ok.Should().BeFalse();
        erro.Should().Contain("atual");
        (await admin.AutenticarAsync(usuario.Email, SenhaInicial)).Should().NotBeNull("a senha em vigor continua valendo");
        (await admin.AutenticarAsync(usuario.Email, "nova-senha-legitima")).Should().BeNull();
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("123456789")]
    public async Task Senha_nova_curta_e_recusada(string curta)
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);

        var (ok, erro) = await Montar(db).TrocarSenhaAsync(usuario.Id, SenhaInicial, curta);

        ok.Should().BeFalse();
        erro.Should().Contain("10 caracteres");
    }

    [Fact]
    public async Task Repetir_a_mesma_senha_nao_conta_como_troca()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);

        var (ok, erro) = await Montar(db).TrocarSenhaAsync(usuario.Id, SenhaInicial, SenhaInicial);

        ok.Should().BeFalse();
        erro.Should().Contain("diferente");
    }

    [Fact]
    public async Task Usuario_desativado_nao_troca_a_propria_senha()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db, ativo: false);

        var (ok, _) = await Montar(db).TrocarSenhaAsync(usuario.Id, SenhaInicial, "nova-senha-legitima");

        ok.Should().BeFalse("acesso revogado não pode virar acesso renovado");
    }

    [Fact]
    public async Task Altera_o_nome_de_exibicao()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);
        var admin = Montar(db);

        var (ok, _) = await admin.AlterarNomeAsync(usuario.Id, "  Maria da Recepção  ");

        ok.Should().BeTrue();
        (await admin.ObterAsync(usuario.Id))!.Nome.Should().Be("Maria da Recepção", "os espaços das pontas saem");
    }

    [Fact]
    public async Task Nome_vazio_e_recusado()
    {
        await using var db = fixture.CriarContexto();
        var usuario = await SemearAsync(db);

        var (ok, _) = await Montar(db).AlterarNomeAsync(usuario.Id, "   ");

        ok.Should().BeFalse();
    }
}
