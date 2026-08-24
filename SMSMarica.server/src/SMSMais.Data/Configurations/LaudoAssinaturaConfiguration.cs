using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Configurations;

internal sealed class LaudoAssinaturaConfiguration : IEntityTypeConfiguration<LaudoAssinatura>
{
    public void Configure(EntityTypeBuilder<LaudoAssinatura> builder)
    {
        builder.ToTable("laudo_assinatura");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.LaudoId).HasColumnName("laudo_id").IsRequired();
        builder.Property(a => a.MedicoId).HasColumnName("medico_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(a => a.PdfAssinado).HasColumnName("pdf_assinado").HasColumnType("bytea");
        builder.Property(a => a.PdfHashSha256).HasColumnName("pdf_hash_sha256").HasColumnType("bytea");
        builder.Property(a => a.TransferState).HasColumnName("transfer_state").HasColumnType("bytea");
        builder.Property(a => a.HashParaAssinar).HasColumnName("hash_para_assinar").HasColumnType("bytea");
        builder.Property(a => a.CertThumbprint).HasColumnName("cert_thumbprint").HasMaxLength(64);
        builder.Property(a => a.EntregueEm).HasColumnName("entregue_em");
        builder.Property(a => a.ChaveAgente).HasColumnName("chave_agente").HasMaxLength(64);
        builder.Property(a => a.ChaveExpiraEm).HasColumnName("chave_expira_em");

        builder.Property(a => a.AssinadoPorCpf).HasColumnName("assinado_por_cpf").HasMaxLength(11);
        builder.Property(a => a.AssinadoPorUsuarioId).HasColumnName("assinado_por_usuario_id");
        builder.Property(a => a.CertificadoTitular).HasColumnName("certificado_titular").HasMaxLength(256);
        builder.Property(a => a.CertificadoEmissor).HasColumnName("certificado_emissor").HasMaxLength(256);
        builder.Property(a => a.Formato).HasColumnName("formato").HasMaxLength(20);
        builder.Property(a => a.ComCarimboTempo).HasColumnName("com_carimbo_tempo").HasDefaultValue(false).IsRequired();

        builder.Property(a => a.AssinadoEm).HasColumnName("assinado_em");
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");

        // xmin nativo do Postgres para concorrência otimista.
        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(a => a.Laudo)
            .WithMany()
            .HasForeignKey(a => a.LaudoId)
            .OnDelete(DeleteBehavior.Restrict);

        // No máximo 1 assinatura concluída por laudo (filtered unique index Postgres).
        builder.HasIndex(a => a.LaudoId)
            .IsUnique()
            .HasFilter($"status = {(int)StatusAssinatura.Concluida}")
            .HasDatabaseName("ix_laudo_assinatura_laudo_id_concluida");

        // Lookup geral por laudo (sessões iniciadas/falhas etc.).
        builder.HasIndex(a => new { a.LaudoId, a.Status });

        // O agente reivindica o job pela chave (capability de uso único).
        builder.HasIndex(a => a.ChaveAgente);
    }
}
