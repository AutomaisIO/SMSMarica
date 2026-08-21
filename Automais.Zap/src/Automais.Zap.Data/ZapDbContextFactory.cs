using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Automais.Zap.Data;

/// <summary>
/// Design-time para <c>dotnet ef</c>. Lê a connection string de <c>ZAP_DB</c>;
/// sem ela, cai num alvo local que só serve para gerar migration.
/// </summary>
public sealed class ZapDbContextFactory : IDesignTimeDbContextFactory<ZapDbContext>
{
    public ZapDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ZAP_DB")
                 ?? "Host=localhost;Port=5432;Database=zap;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ZapDbContext>()
            .UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", ZapDbContext.Schema))
            .Options;

        return new ZapDbContext(options);
    }
}
