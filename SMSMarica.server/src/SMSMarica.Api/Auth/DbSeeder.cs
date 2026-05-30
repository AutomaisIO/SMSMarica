using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Seeds idempotentes executados no startup, depois das migrations.
/// Garante o Admin inicial (perfil + usuário com senha hash).
/// </summary>
public static class DbSeeder
{
    public static Guid AdminUsuarioId => IdentificadoresFixos.UsuarioAdminId;
    private const string AdminEmail = "admin@smsmarica.online";
    private const string AdminSenhaInicial = "Abc,123!";

    public static async Task SeedAsync(
        SmsMaricaDbContext db,
        IPasswordHasher<Usuario> hasher,
        CancellationToken cancellationToken = default)
    {
        await GarantirPerfilAdminAsync(db, cancellationToken);
        await GarantirUsuarioAdminAsync(db, hasher, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task GarantirPerfilAdminAsync(SmsMaricaDbContext db, CancellationToken ct)
    {
        var perfil = await db.Perfis
            .Include(p => p.Permissoes)
            .FirstOrDefaultAsync(p => p.Id == IdentificadoresFixos.PerfilAdminId, ct);

        if (perfil is null)
        {
            perfil = new Perfil
            {
                Id = IdentificadoresFixos.PerfilAdminId,
                Nome = "Administrador",
                Descricao = "Acesso completo a todos os módulos do sistema.",
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
            };
            db.Perfis.Add(perfil);
        }

        // Garante todas as ações em todos os módulos (idempotente).
        var existentes = perfil.Permissoes.ToDictionary(p => p.Modulo);
        foreach (var modulo in Enum.GetValues<ModuloPermissao>())
        {
            if (existentes.TryGetValue(modulo, out var atual))
            {
                if (atual.Acoes != AcoesPermissao.Todas) atual.Acoes = AcoesPermissao.Todas;
            }
            else
            {
                perfil.Permissoes.Add(new PermissaoPerfil
                {
                    PerfilId = perfil.Id,
                    Modulo = modulo,
                    Acoes = AcoesPermissao.Todas,
                });
            }
        }
    }

    private static async Task GarantirUsuarioAdminAsync(
        SmsMaricaDbContext db,
        IPasswordHasher<Usuario> hasher,
        CancellationToken ct)
    {
        var existe = await db.Usuarios
            .Include(u => u.UsuariosPerfis)
            .FirstOrDefaultAsync(u => u.Id == AdminUsuarioId, ct);

        if (existe is null)
        {
            var admin = new Usuario
            {
                Id = AdminUsuarioId,
                NomeExibicao = "Administrador",
                Email = AdminEmail,
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
                SenhaHash = string.Empty,
            };
            admin.SenhaHash = hasher.HashPassword(admin, AdminSenhaInicial);
            admin.UsuariosPerfis.Add(new UsuarioPerfil
            {
                UsuarioId = AdminUsuarioId,
                PerfilId = IdentificadoresFixos.PerfilAdminId,
            });
            db.Usuarios.Add(admin);
            return;
        }

        // Garante o vínculo com o perfil Admin se faltar (não toca a senha).
        if (!existe.UsuariosPerfis.Any(up => up.PerfilId == IdentificadoresFixos.PerfilAdminId))
        {
            existe.UsuariosPerfis.Add(new UsuarioPerfil
            {
                UsuarioId = AdminUsuarioId,
                PerfilId = IdentificadoresFixos.PerfilAdminId,
            });
        }
    }
}
