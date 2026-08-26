using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class PendenciaCadastroConfiguration : IEntityTypeConfiguration<PendenciaCadastro>
{
    public void Configure(EntityTypeBuilder<PendenciaCadastro> builder)
    {
        builder.ToTable("pendencia_cadastro");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.ConversaId).HasColumnName("conversa_id");
        builder.Property(p => p.TelefoneCanonical).HasColumnName("telefone_canonical").HasMaxLength(20).IsRequired();
        builder.Property(p => p.PacienteId).HasColumnName("paciente_id");
        builder.Property(p => p.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(p => p.Vinculo).HasColumnName("vinculo").HasConversion<int>().IsRequired();
        builder.Property(p => p.Observacao).HasColumnName("observacao").HasMaxLength(1000);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.CriadoPor).HasColumnName("criado_por");
        builder.Property(p => p.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(p => p.ResolvidoPor).HasColumnName("resolvido_por");
        builder.Property(p => p.ResolucaoNota).HasColumnName("resolucao_nota").HasMaxLength(1000);

        builder.HasOne(p => p.Conversa)
            .WithMany()
            .HasForeignKey(p => p.ConversaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Fila da tela: abertas primeiro, por data.
        builder.HasIndex(p => new { p.Status, p.CriadoEm })
            .HasDatabaseName("ix_pendencia_cadastro_status_criado");

        builder.HasIndex(p => p.TelefoneCanonical)
            .HasDatabaseName("ix_pendencia_cadastro_telefone");
    }
}
