using SMSMarica.Core.Common.Unidades;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Conversas;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Common;

/// <summary>
/// Paridade da resolução do escopo de unidade (ADR-0033) com o comportamento que estava copiado em
/// <c>SolicitacoesExameService</c>, <c>ConsultasService</c> e <c>LaudosService</c>. Cada divergência
/// entre aquelas cópias seria um vazamento de dado entre unidades — por isso os cinco caminhos da
/// cascata são fixados aqui, incluindo o <b>fail-open</b> (que é comportamento vigente, não desejo).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EscopoUnidadeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Sem_usuario_no_contexto_ve_tudo()
    {
        await using var db = fixture.CriarDbContext();

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake());

        Assert.True(escopo.VeTudo);
        Assert.Null(escopo.Referencia);
    }

    [Fact]
    public async Task Acesso_global_com_unidade_ativa_filtra_por_ela()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: true);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario, unidade));

        Assert.False(escopo.VeTudo);
        Assert.Equal(unidade, escopo.Referencia);
        Assert.Equal([unidade], escopo.Unidades);
    }

    [Fact]
    public async Task Acesso_global_sem_unidade_ativa_ve_tudo()
    {
        await using var db = fixture.CriarDbContext();
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: true);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario));

        Assert.True(escopo.VeTudo);
        Assert.Null(escopo.Referencia);
    }

    /// <summary>
    /// <b>Fail-closed</b> (ADR-0037): usuário autenticado SEM nenhum vínculo não vê nada. Até
    /// 2026-07-30 era o oposto — a ausência de configuração era a permissão mais ampla do sistema.
    /// Este é o teste que impede a regressão.
    /// </summary>
    [Fact]
    public async Task Sem_vinculo_nenhum_nao_ve_nada_fail_closed()
    {
        await using var db = fixture.CriarDbContext();
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: false);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario));

        Assert.False(escopo.VeTudo);
        Assert.True(escopo.SemAcesso);
        Assert.Empty(escopo.Unidades);
    }

    /// <summary>
    /// O passo 1 da cascata continua ABERTO de propósito: sem usuário no contexto não há a quem
    /// restringir, e fechá-lo quebraria a importação SISREG, o sincronismo do Salux e o worker de
    /// comunicação — que rodam sem HttpContext e precisam enxergar a rede inteira.
    /// </summary>
    [Fact]
    public async Task Vinculo_de_unidade_INATIVA_nao_conta_como_vinculo()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: false);
        await VincularAsync(db, usuario, unidade);

        // Desativar a única unidade do usuário o deixa sem escopo — e, com fail-closed, sem ver
        // nada. Antes isso o promovia a "vê tudo", que é o inverso do esperado.
        var u = await db.Unidades.FindAsync(unidade);
        u!.Ativo = false;
        await db.SaveChangesAsync();

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario));

        Assert.True(escopo.SemAcesso);
    }

    [Fact]
    public async Task Ativa_entre_os_vinculos_filtra_por_ela()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: false);
        await VincularAsync(db, usuario, a, b);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario, a));

        Assert.False(escopo.VeTudo);
        Assert.Equal(a, escopo.Referencia);
        Assert.Equal([a], escopo.Unidades);
    }

    [Fact]
    public async Task Sem_ativa_valida_ve_o_conjunto_dos_vinculos_e_sem_referencia()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: false);
        await VincularAsync(db, usuario, a, b);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario));

        Assert.False(escopo.VeTudo);
        // Sem uma unidade única de referência não há seta de direção que faça sentido.
        Assert.Null(escopo.Referencia);
        Assert.Equal(2, escopo.Unidades.Length);
        Assert.Contains(a, escopo.Unidades);
        Assert.Contains(b, escopo.Unidades);
    }

    /// <summary>Unidade ativa que o usuário NÃO tem vínculo não pode virar filtro — senão bastaria
    /// forjar o header X-Unidade-Id para ver a unidade dos outros.</summary>
    [Fact]
    public async Task Ativa_fora_dos_vinculos_cai_no_conjunto()
    {
        await using var db = fixture.CriarDbContext();
        var minha = await CriarUnidadeAsync(db);
        var alheia = await CriarUnidadeAsync(db);
        var usuario = await CriarUsuarioAsync(db, acessoGlobal: false);
        await VincularAsync(db, usuario, minha);

        var escopo = await EscopoUnidade.ResolverAsync(db, new UsuarioAtualAccessorFake(usuario, alheia));

        Assert.Null(escopo.Referencia);
        Assert.Equal([minha], escopo.Unidades);
        Assert.DoesNotContain(alheia, escopo.Unidades);
    }

    // ===================== seed =====================

    private static async Task<Guid> CriarUnidadeAsync(SmsMaricaDbContext db)
    {
        var u = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE {Guid.NewGuid():N}"[..30],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<Guid> CriarUsuarioAsync(SmsMaricaDbContext db, bool acessoGlobal)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "USUARIO TESTE",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            AcessoGlobal = acessoGlobal,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task VincularAsync(SmsMaricaDbContext db, Guid usuarioId, params Guid[] unidades)
    {
        foreach (var id in unidades)
        {
            db.UsuarioUnidades.Add(new UsuarioUnidade
            {
                UsuarioId = usuarioId,
                UnidadeId = id,
                CriadoEm = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }
}
