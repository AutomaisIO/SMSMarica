using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Conversas;

/// <summary>
/// Unidade padrão obrigatória (ADR-0081): todo caminho que mexe nos vínculos ou exige a padrão, ou
/// a cria — nenhum deixa o usuário sem ela.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class UsuarioUnidadeServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Definir_vinculos_exige_exatamente_uma_unidade_padrao()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db);
        var servico = Servico(db);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.DefinirVinculosAsync(usuario, new DefinirVinculosUnidadeRequest([])));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.DefinirVinculosAsync(usuario, new DefinirVinculosUnidadeRequest(
                [new VinculoUnidadeItem(a, false), new VinculoUnidadeItem(b, false)])));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            servico.DefinirVinculosAsync(usuario, new DefinirVinculosUnidadeRequest(
                [new VinculoUnidadeItem(a, true), new VinculoUnidadeItem(b, true)])));

        await servico.DefinirVinculosAsync(usuario, new DefinirVinculosUnidadeRequest(
            [new VinculoUnidadeItem(a, false), new VinculoUnidadeItem(b, true)]));
        Assert.Equal(b, await servico.ObterPrincipalIdAsync(usuario));
    }

    [Fact]
    public async Task Incluir_pela_unidade_quem_nao_tem_padrao_faz_dela_a_padrao()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db);
        var servico = Servico(db);

        await servico.AdicionarUsuarioAsync(a, usuario);
        await servico.AdicionarUsuarioAsync(b, usuario);

        Assert.Equal(a, await servico.ObterPrincipalIdAsync(usuario));
        Assert.Equal(2, (await servico.ObterVinculosAsync(usuario)).Count);
    }

    [Fact]
    public async Task Remover_pela_unidade_nao_tira_a_padrao()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db);
        var servico = Servico(db);
        await servico.DefinirVinculosAsync(usuario, new DefinirVinculosUnidadeRequest(
            [new VinculoUnidadeItem(a, true), new VinculoUnidadeItem(b, false)]));

        await Assert.ThrowsAsync<ValidacaoException>(() => servico.RemoverUsuarioAsync(a, usuario));
        await servico.RemoverUsuarioAsync(b, usuario);

        var restante = Assert.Single(await servico.ObterVinculosAsync(usuario));
        Assert.Equal(a, restante.UnidadeId);
        Assert.True(restante.Principal);
    }

    [Fact]
    public async Task Unidade_unica_sem_marca_nao_conta_como_padrao()
    {
        // A migration UnidadePadraoObrigatoria marca esses casos; depois dela, só a marcada vale.
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db);
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario,
            UnidadeId = a,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Null(await Servico(db).ObterPrincipalIdAsync(usuario));
        Assert.True(await db.UsuarioUnidades.AnyAsync(v => v.UsuarioId == usuario));
    }

    // ------------------------------------------------------------------ apoio

    private static UsuarioUnidadeService Servico(SmsMaisDbContext db) =>
        new(db, new UsuarioAtualAccessorFake(Guid.NewGuid()));

    private static async Task<Guid> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID {Guid.NewGuid():N}"[..24],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private static async Task<Guid> CriarUsuarioAsync(SmsMaisDbContext db)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"PESSOA {Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }
}
