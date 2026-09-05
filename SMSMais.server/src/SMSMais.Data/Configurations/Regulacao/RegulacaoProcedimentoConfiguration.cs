using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoProcedimentoConfiguration
    : IEntityTypeConfiguration<RegulacaoProcedimento>
{
    public void Configure(EntityTypeBuilder<RegulacaoProcedimento> builder)
    {
        builder.ToTable("regulacao_procedimento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.NomeCanonico).HasColumnName("nome_canonico").HasMaxLength(300).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.ProcedimentoSigtapId).HasColumnName("procedimento_sigtap_id");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        builder.HasIndex(x => x.NomeNormalizado)
            .HasDatabaseName("ix_regulacao_procedimento_nome_normalizado");

        // `SetNull`: o correlato SIGTAP é informativo, não é dono. Se a linha do SIGTAP sair,
        // o canônico continua válido — só perde o correlato. Cascatear levaria junto a origem
        // que a solicitação aponta, e `Restrict` faria o SIGTAP virar refém do catálogo.
        builder.HasOne<ProcedimentoSigtap>()
            .WithMany()
            .HasForeignKey(x => x.ProcedimentoSigtapId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
