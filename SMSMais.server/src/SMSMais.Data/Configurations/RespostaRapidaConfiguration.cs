using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Conversas;

namespace SMSMais.Data.Configurations;

internal sealed class RespostaRapidaConfiguration : IEntityTypeConfiguration<RespostaRapida>
{
    public void Configure(EntityTypeBuilder<RespostaRapida> builder)
    {
        builder.ToTable("resposta_rapida");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Titulo).HasColumnName("titulo").HasMaxLength(120).IsRequired();
        builder.Property(r => r.Corpo).HasColumnName("corpo").HasMaxLength(4000).IsRequired();
        builder.Property(r => r.Categoria).HasColumnName("categoria").HasMaxLength(60);
        builder.Property(r => r.UnidadeId).HasColumnName("unidade_id");
        builder.Property(r => r.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(r => r.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();

        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(r => r.CriadoPor).HasColumnName("criado_por");
        builder.Property(r => r.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(r => r.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(r => r.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(r => r.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(r => r.Unidade)
            .WithMany()
            .HasForeignKey(r => r.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Campos)
            .WithOne(c => c.RespostaRapida!)
            .HasForeignKey(c => c.RespostaRapidaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lista lateral do chat: globais + as da unidade do operador, na ordem definida.
        builder.HasIndex(r => new { r.UnidadeId, r.Ordem });
    }
}

internal sealed class RespostaRapidaCampoConfiguration : IEntityTypeConfiguration<RespostaRapidaCampo>
{
    public void Configure(EntityTypeBuilder<RespostaRapidaCampo> builder)
    {
        builder.ToTable("resposta_rapida_campo");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RespostaRapidaId).HasColumnName("resposta_rapida_id").IsRequired();
        builder.Property(c => c.Nome).HasColumnName("nome").HasMaxLength(40).IsRequired();
        builder.Property(c => c.Rotulo).HasColumnName("rotulo").HasMaxLength(80);
        builder.Property(c => c.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(c => c.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();

        // O corpo casa a tag pelo nome — duas iguais na mesma mensagem seriam ambíguas.
        builder.HasIndex(c => new { c.RespostaRapidaId, c.Nome }).IsUnique();
    }
}
