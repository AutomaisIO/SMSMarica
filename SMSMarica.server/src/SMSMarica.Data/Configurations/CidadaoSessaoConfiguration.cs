using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class CidadaoSessaoConfiguration : IEntityTypeConfiguration<CidadaoSessao>
{
    public void Configure(EntityTypeBuilder<CidadaoSessao> builder)
    {
        builder.ToTable("cidadao_sessao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CidadaoAcessoId).HasColumnName("cidadao_acesso_id").IsRequired();
        builder.Property(s => s.Canal).HasColumnName("canal").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Dispositivo).HasColumnName("dispositivo").HasMaxLength(255);
        builder.Property(s => s.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(s => s.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(s => s.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(s => s.RevogadaEm).HasColumnName("revogada_em");

        // Busca quente: validar o jti (PK) já é por chave. Este índice acelera
        // "achar a sessão ativa do acesso" na hora de revogar no novo login.
        builder.HasIndex(s => new { s.CidadaoAcessoId, s.RevogadaEm })
            .HasDatabaseName("ix_cidadao_sessao_acesso_ativa");
    }
}
