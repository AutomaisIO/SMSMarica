using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Data.Configurations.Ia;

internal sealed class IaFonteConfiguration : IEntityTypeConfiguration<IaFonte>
{
    public void Configure(EntityTypeBuilder<IaFonte> builder)
    {
        builder.ToTable("ia_fonte");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(x => x.Dialeto).HasColumnName("dialeto").HasConversion<int>().IsRequired();
        builder.Property(x => x.Ambiente).HasColumnName("ambiente").HasConversion<int>().IsRequired();
        builder.Property(x => x.Host).HasColumnName("host").HasMaxLength(200);
        builder.Property(x => x.Porta).HasColumnName("porta");
        builder.Property(x => x.Servico).HasColumnName("servico").HasMaxLength(120);
        builder.Property(x => x.Usuario).HasColumnName("usuario").HasMaxLength(120);
        builder.Property(x => x.SenhaCifrada).HasColumnName("senha_cifrada");
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(300);
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasIndex(x => x.Nome);
    }
}
