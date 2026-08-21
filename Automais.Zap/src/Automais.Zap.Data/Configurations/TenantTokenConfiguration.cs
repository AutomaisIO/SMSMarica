using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class TenantTokenConfiguration : IEntityTypeConfiguration<TenantToken>
{
    public void Configure(EntityTypeBuilder<TenantToken> b)
    {
        b.ToTable("tenant_token");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        b.Property(x => x.Prefixo).HasColumnName("prefixo").HasMaxLength(20).IsRequired();
        b.Property(x => x.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.TodosNumeros).HasColumnName("todos_numeros").HasDefaultValue(true);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.UltimoUsoEm).HasColumnName("ultimo_uso_em");
        b.Property(x => x.RevogadoEm).HasColumnName("revogado_em");
        b.Ignore(x => x.Ativo);

        // A autenticação busca pelo prefixo antes de conferir o hash: sem índice único aqui,
        // seria varredura da tabela a cada chamada de API.
        b.HasIndex(x => x.Prefixo).IsUnique().HasDatabaseName("ux_tenant_token_prefixo");

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TenantTokenNumeroConfiguration : IEntityTypeConfiguration<TenantTokenNumero>
{
    public void Configure(EntityTypeBuilder<TenantTokenNumero> b)
    {
        b.ToTable("tenant_token_numero");
        b.HasKey(x => new { x.TokenId, x.NumeroId });
        b.Property(x => x.TokenId).HasColumnName("token_id");
        b.Property(x => x.NumeroId).HasColumnName("numero_id");

        b.HasOne(x => x.Token).WithMany(t => t.Numeros)
            .HasForeignKey(x => x.TokenId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Numero).WithMany()
            .HasForeignKey(x => x.NumeroId).OnDelete(DeleteBehavior.Cascade);
    }
}
