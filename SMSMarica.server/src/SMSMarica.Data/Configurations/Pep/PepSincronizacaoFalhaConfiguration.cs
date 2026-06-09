using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Data.Configurations.Pep;

internal sealed class PepSincronizacaoFalhaConfiguration : IEntityTypeConfiguration<PepSincronizacaoFalha>
{
    public void Configure(EntityTypeBuilder<PepSincronizacaoFalha> builder)
    {
        builder.ToTable("pep_sincronizacao_falha");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id").IsRequired();
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.FonteSlug).HasColumnName("fonte_slug").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CdPaciente).HasColumnName("cd_paciente").IsRequired();
        builder.Property(x => x.Mensagem).HasColumnName("mensagem").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.ResolvidoEm).HasColumnName("resolvido_em");

        builder.HasIndex(x => x.ExecucaoId).HasDatabaseName("ix_pep_falha_execucao");

        // Falhas pendentes por base — base do reimport direcionado.
        builder.HasIndex(x => new { x.FonteId, x.ResolvidoEm }).HasDatabaseName("ix_pep_falha_fonte_resolvido");
    }
}
