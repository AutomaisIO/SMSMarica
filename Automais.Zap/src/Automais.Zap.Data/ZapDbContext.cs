using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Data;

/// <summary>
/// Banco do relay. Vive num Postgres LOCAL do droplet do Automais.Zap — não no cluster
/// gerenciado que as instâncias usam. O relay é ponto único de falha do inbound de todos
/// os clientes; não pode herdar o destino de um vizinho barulhento no cluster compartilhado.
/// </summary>
public sealed class ZapDbContext(DbContextOptions<ZapDbContext> options) : DbContext(options)
{
    public const string Schema = "zap";

    public DbSet<Destino> Destinos => Set<Destino>();
    public DbSet<Numero> Numeros => Set<Numero>();
    public DbSet<UsuarioAdmin> UsuariosAdmin => Set<UsuarioAdmin>();
    public DbSet<EntregaLog> EntregasLog => Set<EntregaLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZapDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
