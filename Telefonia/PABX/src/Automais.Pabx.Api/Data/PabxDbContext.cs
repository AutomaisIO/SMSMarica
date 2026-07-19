using Automais.Pabx.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Automais.Pabx.Api.Data;

public sealed class PabxDbContext(DbContextOptions<PabxDbContext> options) : DbContext(options)
{
    public DbSet<Unidade> Unidades => Set<Unidade>();
    public DbSet<Ramal> Ramais => Set<Ramal>();
    public DbSet<ArquivoGerenciado> ArquivosGerenciados => Set<ArquivoGerenciado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unidade>(e =>
        {
            e.ToTable("unidade");
            e.Property(u => u.Id).ValueGeneratedNever(); // id vem do plano de endereçamento
            e.Property(u => u.Nome).HasMaxLength(200);
        });

        modelBuilder.Entity<Ramal>(e =>
        {
            e.ToTable("ramal");
            e.HasIndex(r => r.Numero).IsUnique();
            e.HasIndex(r => r.Mac).IsUnique().HasFilter("mac IS NOT NULL");
            e.Property(r => r.Numero).HasMaxLength(10);
            e.Property(r => r.Mac).HasMaxLength(12).HasColumnName("mac");
            e.Property(r => r.Marca).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Origem).HasConversion<string>().HasMaxLength(20);
            e.HasOne(r => r.Unidade).WithMany(u => u.Ramais).HasForeignKey(r => r.UnidadeId);
        });

        modelBuilder.Entity<ArquivoGerenciado>(e =>
        {
            e.ToTable("arquivo_gerenciado");
            e.HasIndex(a => a.Caminho).IsUnique();
            e.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(30);
            e.HasOne(a => a.Ramal).WithMany().HasForeignKey(a => a.RamalId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
