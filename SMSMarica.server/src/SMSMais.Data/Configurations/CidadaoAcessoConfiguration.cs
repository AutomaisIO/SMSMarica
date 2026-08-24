using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoAcessoConfiguration : IEntityTypeConfiguration<CidadaoAcesso>
{
    public void Configure(EntityTypeBuilder<CidadaoAcesso> builder)
    {
        builder.ToTable("cidadao_acesso");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(a => a.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(a => a.SenhaHash).HasColumnName("senha_hash").HasMaxLength(256);
        builder.Property(a => a.GoogleSub).HasColumnName("google_sub").HasMaxLength(255);
        builder.Property(a => a.MicrosoftSub).HasColumnName("microsoft_sub").HasMaxLength(255);
        builder.Property(a => a.FacebookSub).HasColumnName("facebook_sub").HasMaxLength(255);
        builder.Property(a => a.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(a => a.Cpf).IsUnique().HasDatabaseName("ux_cidadao_acesso_cpf");
        builder.HasIndex(a => a.PatientId).IsUnique().HasDatabaseName("ux_cidadao_acesso_patient");

        // Vínculos sociais únicos quando preenchidos (índice filtrado: ignora NULL).
        builder.HasIndex(a => a.GoogleSub).IsUnique()
            .HasDatabaseName("ux_cidadao_acesso_google").HasFilter("google_sub IS NOT NULL");
        builder.HasIndex(a => a.MicrosoftSub).IsUnique()
            .HasDatabaseName("ux_cidadao_acesso_microsoft").HasFilter("microsoft_sub IS NOT NULL");
        builder.HasIndex(a => a.FacebookSub).IsUnique()
            .HasDatabaseName("ux_cidadao_acesso_facebook").HasFilter("facebook_sub IS NOT NULL");

        builder.HasMany(a => a.Sessoes)
            .WithOne(s => s.CidadaoAcesso)
            .HasForeignKey(s => s.CidadaoAcessoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
