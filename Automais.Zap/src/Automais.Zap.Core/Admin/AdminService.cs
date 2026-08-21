using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Automais.Zap.Core.Admin;

public sealed class AdminOptions
{
    public const string Secao = "Admin";

    /// <summary>
    /// Quem tem e-mail neste domínio nasce global — enxerga todos os tenants e alterna entre
    /// eles. É avaliado só na criação: quem pode criar usuário pode conceder acesso global,
    /// então criar usuário é ação restrita a quem já é global.
    /// </summary>
    public string DominioGlobal { get; set; } = "@automais.com";

    public string? Email { get; set; }
    public string? SenhaInicial { get; set; }
}

public sealed record UsuarioListado(
    Guid Id, string Email, string Nome, bool Global, bool Ativo,
    DateTimeOffset? UltimoAcessoEm, IReadOnlyList<string> Tenants);

public interface IAdminService
{
    Task<UsuarioAdmin?> AutenticarAsync(string email, string senha, CancellationToken ct = default);
    Task SemearPrimeiroUsuarioAsync(CancellationToken ct = default);

    Task<IReadOnlyList<UsuarioListado>> ListarUsuariosAsync(Guid? tenantId = null, CancellationToken ct = default);
    Task<(bool Ok, string? Erro)> CriarUsuarioAsync(string email, string nome, string senha, Guid? tenantId, CancellationToken ct = default);
    Task VincularAsync(Guid usuarioId, Guid tenantId, CancellationToken ct = default);
    Task DesvincularAsync(Guid usuarioId, Guid tenantId, CancellationToken ct = default);
    Task AlternarAtivoAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Tenants que o usuário enxerga. Global vê todos.</summary>
    Task<IReadOnlyList<Tenant>> TenantsVisiveisAsync(Guid usuarioId, bool global, CancellationToken ct = default);
}

public sealed class AdminService(
    ZapDbContext db,
    IOptions<AdminOptions> opcoes,
    TimeProvider relogio,
    ILogger<AdminService> logger) : IAdminService
{
    public async Task<UsuarioAdmin?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        var usuario = await db.UsuariosAdmin.FirstOrDefaultAsync(u => u.Email == normalizado && u.Ativo, ct);

        // Confere a senha mesmo sem usuário, contra um hash descartável, para que "e-mail não
        // existe" e "senha errada" levem o mesmo tempo.
        if (usuario is null)
        {
            Senhas.Confere(senha, Senhas.Gerar("descartavel"));
            return null;
        }

        if (!Senhas.Confere(senha, usuario.SenhaHash)) return null;

        usuario.UltimoAcessoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return usuario;
    }

    public async Task SemearPrimeiroUsuarioAsync(CancellationToken ct = default)
    {
        var email = opcoes.Value.Email;
        var senha = opcoes.Value.SenhaInicial;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return;
        if (await db.UsuariosAdmin.AnyAsync(ct)) return;

        // O primeiro operador é SEMPRE global, mesmo com e-mail fora do domínio da casa.
        // Sem isso, semear com um endereço pessoal criaria um painel sem ninguém que enxergue
        // tenant nenhum — e sem como consertar pela tela.
        db.UsuariosAdmin.Add(new UsuarioAdmin
        {
            Email = Normalizar(email),
            Nome = Normalizar(email),
            SenhaHash = Senhas.Gerar(senha),
            Global = true,
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Primeiro operador criado ({Email}), como global. Troque a senha inicial.", Normalizar(email));
    }

    public async Task<IReadOnlyList<UsuarioListado>> ListarUsuariosAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var consulta = db.UsuariosAdmin.AsNoTracking().AsQueryable();
        if (tenantId is not null)
        {
            consulta = consulta.Where(u => u.Tenants.Any(t => t.TenantId == tenantId));
        }

        return await consulta
            .OrderByDescending(u => u.Global).ThenBy(u => u.Email)
            .Select(u => new UsuarioListado(
                u.Id, u.Email, u.Nome, u.Global, u.Ativo, u.UltimoAcessoEm,
                u.Tenants.Select(t => t.Tenant!.Nome).OrderBy(n => n).ToList()))
            .ToListAsync(ct);
    }

    public async Task<(bool Ok, string? Erro)> CriarUsuarioAsync(
        string email, string nome, string senha, Guid? tenantId, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        if (normalizado.Length == 0 || !normalizado.Contains('@')) return (false, "E-mail inválido.");
        if (senha.Length < 10) return (false, "A senha precisa de pelo menos 10 caracteres.");
        if (await db.UsuariosAdmin.AnyAsync(u => u.Email == normalizado, ct)) return (false, "Já existe usuário com esse e-mail.");

        var global = normalizado.EndsWith(opcoes.Value.DominioGlobal, StringComparison.OrdinalIgnoreCase);
        if (!global && tenantId is null) return (false, "Usuário fora do domínio da casa precisa de um tenant.");

        var usuario = new UsuarioAdmin
        {
            Email = normalizado,
            Nome = string.IsNullOrWhiteSpace(nome) ? normalizado : nome.Trim(),
            SenhaHash = Senhas.Gerar(senha),
            Global = global,
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        };
        db.UsuariosAdmin.Add(usuario);

        if (!global && tenantId is not null)
        {
            db.UsuariosTenant.Add(new UsuarioTenant
            {
                Usuario = usuario,
                TenantId = tenantId.Value,
                CriadoEm = relogio.GetUtcNow(),
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Usuário {Email} criado (global={Global}).", normalizado, global);
        return (true, null);
    }

    public async Task VincularAsync(Guid usuarioId, Guid tenantId, CancellationToken ct = default)
    {
        if (await db.UsuariosTenant.AnyAsync(x => x.UsuarioId == usuarioId && x.TenantId == tenantId, ct)) return;

        db.UsuariosTenant.Add(new UsuarioTenant
        {
            UsuarioId = usuarioId,
            TenantId = tenantId,
            CriadoEm = relogio.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task DesvincularAsync(Guid usuarioId, Guid tenantId, CancellationToken ct = default)
    {
        await db.UsuariosTenant
            .Where(x => x.UsuarioId == usuarioId && x.TenantId == tenantId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task AlternarAtivoAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.UsuariosAdmin.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null) return;

        usuario.Ativo = !usuario.Ativo;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Tenant>> TenantsVisiveisAsync(Guid usuarioId, bool global, CancellationToken ct = default)
    {
        var consulta = db.Tenants.AsNoTracking().AsQueryable();
        if (!global)
        {
            consulta = consulta.Where(t => t.Usuarios.Any(u => u.UsuarioId == usuarioId));
        }

        return await consulta.OrderBy(t => t.Nome).ToListAsync(ct);
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}
