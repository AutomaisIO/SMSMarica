using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregConfiguracaoConfiguration : IEntityTypeConfiguration<SisregConfiguracao>
{
    public void Configure(EntityTypeBuilder<SisregConfiguracao> builder)
    {
        builder.ToTable("sisreg_configuracao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Escopo).HasColumnName("escopo").HasConversion<int>().IsRequired();
        builder.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CentraisReguladoras).HasColumnName("centrais_reguladoras").HasMaxLength(500).IsRequired();
        builder.Property(x => x.TipoAutenticacao).HasColumnName("tipo_autenticacao").HasConversion<int>().IsRequired();
        builder.Property(x => x.Login).HasColumnName("login").HasMaxLength(200);
        builder.Property(x => x.SenhaCifrada).HasColumnName("senha_cifrada");
        builder.Property(x => x.TokenCifrado).HasColumnName("token_cifrado");
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
