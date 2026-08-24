using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class LaudoTemplateConfiguration : IEntityTypeConfiguration<LaudoTemplate>
{
    public void Configure(EntityTypeBuilder<LaudoTemplate> builder)
    {
        builder.ToTable("laudo_template");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Categoria).HasColumnName("categoria").HasMaxLength(80).IsRequired();
        builder.Property(t => t.Descricao).HasColumnName("descricao").HasMaxLength(500);
        builder.Property(t => t.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.ConteudoHtml).HasColumnName("conteudo_html").HasColumnType("text").IsRequired();
        builder.Property(t => t.EstruturaJson).HasColumnName("estrutura_json").HasColumnType("jsonb");

        builder.Property(t => t.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id").IsRequired();
        builder.Property(t => t.AtualizadoPorUsuarioId).HasColumnName("atualizado_por_usuario_id");
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();

        builder.HasOne(t => t.CriadoPorUsuario)
            .WithMany()
            .HasForeignKey(t => t.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AtualizadoPorUsuario)
            .WithMany()
            .HasForeignKey(t => t.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nome único entre templates ativos.
        builder.HasIndex(t => t.Nome)
            .IsUnique()
            .HasFilter("ativo = true");

        builder.HasIndex(t => t.Categoria);
        builder.HasIndex(t => t.Ativo);
    }
}
