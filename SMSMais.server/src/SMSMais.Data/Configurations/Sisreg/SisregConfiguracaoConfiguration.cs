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
        // Sem HasDefaultValue de propósito, ao contrário de `fonte_cadastro_paciente` logo abaixo:
        // num `bool`, o sentinela do EF é `false` (o default do CLR), então uma coluna com default
        // de banco `true` faria um INSERT com o valor `false` gravar `true` — o desligamento sumiria
        // exatamente na linha que ele precisa criar. A linha singleton que já existe em produção é
        // preenchida com `true` pela própria migration. Mesmo tratamento de `ativo`.
        builder.Property(x => x.SincronismoAutomaticoAtivo)
            .HasColumnName("sincronismo_automatico_ativo").IsRequired();
        builder.Property(x => x.FonteCadastroPaciente)
            .HasColumnName("fonte_cadastro_paciente").HasConversion<int>().IsRequired()
            // Default no BANCO, não só no POCO: a linha singleton já existe em produção e uma
            // coluna nova sem default nasceria NULL/0 — que não é nenhuma fonte válida.
            .HasDefaultValue(Entities.Enums.FonteCadastroPaciente.Sisreg);
        builder.Property(x => x.ConsultasSimultaneasSer)
            .HasColumnName("consultas_simultaneas_ser").IsRequired().HasDefaultValue(1);

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
