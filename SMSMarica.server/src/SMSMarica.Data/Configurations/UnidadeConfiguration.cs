using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        builder.ToTable("unidade");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(u => u.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(u => u.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(u => u.Externa).HasColumnName("externa").HasDefaultValue(false).IsRequired();
        builder.Property(u => u.CodigoIbgeCidade).HasColumnName("codigo_ibge_cidade").HasMaxLength(7);

        builder.OwnsOne(u => u.Endereco, e =>
        {
            e.Property(x => x.Cep).HasColumnName("endereco_cep").HasMaxLength(8);
            e.Property(x => x.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            e.Property(x => x.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            e.Property(x => x.Complemento).HasColumnName("endereco_complemento").HasMaxLength(120);
            e.Property(x => x.Bairro).HasColumnName("endereco_bairro").HasMaxLength(120);
            e.Property(x => x.Cidade).HasColumnName("endereco_cidade").HasMaxLength(120);
            e.Property(x => x.Uf).HasColumnName("endereco_uf").HasMaxLength(2);
            e.Property(x => x.PontoReferencia).HasColumnName("endereco_ponto_referencia").HasMaxLength(200);
        });

        builder.OwnsOne(u => u.Gps, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("latitude");
            gps.Property(g => g.Longitude).HasColumnName("longitude");
        });
    }
}
