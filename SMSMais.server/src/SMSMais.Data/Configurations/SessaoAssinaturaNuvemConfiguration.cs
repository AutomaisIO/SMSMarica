using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class SessaoAssinaturaNuvemConfiguration : IEntityTypeConfiguration<SessaoAssinaturaNuvem>
{
    public void Configure(EntityTypeBuilder<SessaoAssinaturaNuvem> builder)
    {
        builder.ToTable("assinatura_nuvem_sessao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.MedicoId).HasColumnName("medico_id").IsRequired();
        builder.Property(s => s.SessaoLoginId).HasColumnName("sessao_login_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.CredencialId).HasColumnName("credencial_id").HasMaxLength(128).IsRequired();
        builder.Property(s => s.CodeVerifier).HasColumnName("code_verifier").HasColumnType("text");
        builder.Property(s => s.CertThumbprint).HasColumnName("cert_thumbprint").HasMaxLength(64);
        builder.Property(s => s.AutorizadaEm).HasColumnName("autorizada_em").IsRequired();
        builder.Property(s => s.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(s => s.AutorizadaPorUsuarioId).HasColumnName("autorizada_por_usuario_id");
        builder.Property(s => s.EncerradaEm).HasColumnName("encerrada_em");
        builder.Property(s => s.MotivoEncerramento).HasColumnName("motivo_encerramento").HasMaxLength(64);
        builder.Property(s => s.EncerradaPorUsuarioId).HasColumnName("encerrada_por_usuario_id");

        // No máximo UMA sessão aberta por login do médico: autorizar de novo substitui a
        // anterior. Logins diferentes (dois computadores) têm cada um a sua. O login vem
        // primeiro: o logout procura só por ele.
        builder.HasIndex(s => new { s.SessaoLoginId, s.MedicoId })
            .IsUnique()
            .HasFilter("encerrada_em IS NULL")
            .HasDatabaseName("ix_assinatura_nuvem_sessao_login_aberta");
    }
}
