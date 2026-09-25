using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class CampanhaConfiguration : IEntityTypeConfiguration<Campanha>
{
    public void Configure(EntityTypeBuilder<Campanha> builder)
    {
        builder.ToTable("campanha");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.InicioEm).HasColumnName("inicio_em").IsRequired();
        builder.Property(x => x.FimEm).HasColumnName("fim_em").IsRequired();
        builder.Property(x => x.LocalNome).HasColumnName("local_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.LocalEndereco).HasColumnName("local_endereco").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ExigirConferenciaCadastral).HasColumnName("exigir_conferencia_cadastral").IsRequired();
        builder.Property(x => x.EnvioAutomatico).HasColumnName("envio_automatico").IsRequired();
        builder.Property(x => x.Ativa).HasColumnName("ativa").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(x => x.Unidade).WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UnidadeId, x.InicioEm, x.FimEm })
            .HasDatabaseName("ix_campanha_unidade_periodo");

        builder.ToTable(t => t.HasCheckConstraint("ck_campanha_periodo", "fim_em > inicio_em"));
    }
}
