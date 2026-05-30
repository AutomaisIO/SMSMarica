using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", t =>
        {
            // Garante que no máximo uma das três FKs de papel esteja setada.
            // (Pessoa sem papel — admin/operador — também é válida; daí "<= 1".)
            t.HasCheckConstraint(
                "ck_usuario_papel_unico",
                "(CASE WHEN patient_id IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN practitioner_id IS NOT NULL THEN 1 ELSE 0 END + " +
                "CASE WHEN motorista_id IS NOT NULL THEN 1 ELSE 0 END) <= 1");
        });
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(u => u.SenhaHash).HasColumnName("senha_hash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.DeveTrocarSenha).HasColumnName("deve_trocar_senha").HasDefaultValue(false).IsRequired();
        builder.Property(u => u.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(u => u.UltimoAcessoEm).HasColumnName("ultimo_acesso_em");
        builder.Property(u => u.NomeExibicao).HasColumnName("nome_exibicao").HasMaxLength(200).IsRequired();

        // Auditoria
        builder.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(u => u.CriadoPor).HasColumnName("criado_por");
        builder.Property(u => u.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(u => u.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(u => u.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(u => u.ExcluidoPor).HasColumnName("excluido_por");

        // FKs de papel
        builder.Property(u => u.PatientId).HasColumnName("patient_id");
        builder.Property(u => u.PractitionerId).HasColumnName("practitioner_id");
        builder.Property(u => u.MotoristaId).HasColumnName("motorista_id");

        // Cross-schema FK → fhir.patient
        builder.HasOne(u => u.Patient)
            .WithMany()
            .HasForeignKey(u => u.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cross-schema FK → fhir.practitioner
        builder.HasOne(u => u.Practitioner)
            .WithMany()
            .HasForeignKey(u => u.PractitionerId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → smsmarica.motorista
        builder.HasOne(u => u.Motorista)
            .WithMany()
            .HasForeignKey(u => u.MotoristaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.PatientId).IsUnique().HasFilter("patient_id IS NOT NULL");
        builder.HasIndex(u => u.PractitionerId).IsUnique().HasFilter("practitioner_id IS NOT NULL");
        builder.HasIndex(u => u.MotoristaId).IsUnique().HasFilter("motorista_id IS NOT NULL");
        builder.HasIndex(u => u.ExcluidoEm)
            .HasDatabaseName("ix_usuario_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
