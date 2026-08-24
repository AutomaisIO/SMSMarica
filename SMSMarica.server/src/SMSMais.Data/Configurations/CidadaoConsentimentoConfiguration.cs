using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoConsentimentoConfiguration : IEntityTypeConfiguration<CidadaoConsentimento>
{
    public void Configure(EntityTypeBuilder<CidadaoConsentimento> builder)
    {
        builder.ToTable("cidadao_consentimento");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.CidadaoAcessoId).HasColumnName("cidadao_acesso_id").IsRequired();
        builder.Property(c => c.Versao).HasColumnName("versao").HasMaxLength(20).IsRequired();
        builder.Property(c => c.TextoHash).HasColumnName("texto_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.AceitoEm).HasColumnName("aceito_em").IsRequired();
        builder.Property(c => c.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(c => c.Dispositivo).HasColumnName("dispositivo").HasMaxLength(255);
        builder.Property(c => c.RevogadoEm).HasColumnName("revogado_em");

        builder.HasOne(c => c.CidadaoAcesso)
            .WithMany(a => a.Consentimentos)
            .HasForeignKey(c => c.CidadaoAcessoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Busca quente do gate: "tem consentimento ativo da versão vigente p/ este acesso?".
        builder.HasIndex(c => new { c.CidadaoAcessoId, c.Versao, c.RevogadoEm })
            .HasDatabaseName("ix_cidadao_consentimento_vigente");
    }
}
