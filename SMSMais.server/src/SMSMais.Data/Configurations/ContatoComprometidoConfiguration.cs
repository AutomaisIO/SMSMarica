using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class ContatoComprometidoConfiguration : IEntityTypeConfiguration<ContatoComprometido>
{
    public void Configure(EntityTypeBuilder<ContatoComprometido> builder)
    {
        builder.ToTable("contato_comprometido");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(c => c.TelefoneCanonical).HasColumnName("telefone_canonical").HasMaxLength(20).IsRequired();
        builder.Property(c => c.Motivo).HasColumnName("motivo").HasConversion<int>().IsRequired();
        builder.Property(c => c.Detalhe).HasColumnName("detalhe").HasMaxLength(500);
        builder.Property(c => c.ComunicacaoId).HasColumnName("comunicacao_id");
        builder.Property(c => c.DescobertoEm).HasColumnName("descoberto_em").IsRequired();
        builder.Property(c => c.UltimaOcorrenciaEm).HasColumnName("ultima_ocorrencia_em").IsRequired();
        builder.Property(c => c.Ocorrencias).HasColumnName("ocorrencias").IsRequired();
        builder.Property(c => c.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(c => c.ResolvidoPor).HasColumnName("resolvido_por");
        builder.Property(c => c.ResolucaoNota).HasColumnName("resolucao_nota").HasMaxLength(1000);

        // Uma marca ABERTA por paciente × número × motivo — reincidência vira contador, não linha nova.
        builder.HasIndex(c => new { c.PacienteId, c.TelefoneCanonical, c.Motivo })
            .HasDatabaseName("ux_contato_comprometido_aberto")
            .HasFilter("resolvido_em IS NULL")
            .IsUnique();

        // A aba pergunta "quem está comprometido hoje", por paciente.
        builder.HasIndex(c => new { c.ResolvidoEm, c.PacienteId })
            .HasDatabaseName("ix_contato_comprometido_aberto_paciente");
    }
}
