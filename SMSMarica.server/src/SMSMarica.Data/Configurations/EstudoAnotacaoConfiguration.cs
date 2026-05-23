using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class EstudoAnotacaoConfiguration : IEntityTypeConfiguration<EstudoAnotacao>
{
    public void Configure(EntityTypeBuilder<EstudoAnotacao> builder)
    {
        builder.ToTable("estudo_anotacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.StudyInstanceUID)
            .HasColumnName("study_instance_uid")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(a => a.Versao).HasColumnName("versao").IsRequired();
        builder.Property(a => a.PayloadJson)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(a => a.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.Comentario).HasColumnName("comentario").HasMaxLength(500);

        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Garante unicidade da versão por estudo e acelera "última versão" e "histórico por estudo".
        builder.HasIndex(a => new { a.StudyInstanceUID, a.Versao }).IsUnique();
    }
}
