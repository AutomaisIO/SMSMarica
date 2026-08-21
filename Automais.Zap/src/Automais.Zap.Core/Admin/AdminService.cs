using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Admin;

public interface IAdminService
{
    Task<UsuarioAdmin?> AutenticarAsync(string email, string senha, CancellationToken ct = default);

    /// <summary>
    /// Cria o primeiro operador a partir de <c>Admin:Email</c>/<c>Admin:SenhaInicial</c>, e só
    /// se a tabela estiver vazia. Nunca sobrescreve nem redefine senha de quem já existe.
    /// </summary>
    Task SemearPrimeiroUsuarioAsync(string? email, string? senha, CancellationToken ct = default);
}

public sealed class AdminService(
    ZapDbContext db,
    TimeProvider relogio,
    ILogger<AdminService> logger) : IAdminService
{
    public async Task<UsuarioAdmin?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();
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

    public async Task SemearPrimeiroUsuarioAsync(string? email, string? senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return;
        if (await db.UsuariosAdmin.AnyAsync(ct)) return;

        var normalizado = email.Trim().ToLowerInvariant();
        db.UsuariosAdmin.Add(new UsuarioAdmin
        {
            Email = normalizado,
            Nome = normalizado,
            SenhaHash = Senhas.Gerar(senha),
            Ativo = true,
            CriadoEm = relogio.GetUtcNow(),
        });

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Primeiro operador criado ({Email}). Troque a senha inicial.", normalizado);
    }
}
