using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoMedicoLocalConfiguration : IEntityTypeConfiguration<RegulacaoMedicoLocal>
{
    public void Configure(EntityTypeBuilder<RegulacaoMedicoLocal> builder)
    {
        builder.ToTable("regulacao_medico_local");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(x => x.Conselho).HasColumnName("conselho").HasMaxLength(20);
        builder.Property(x => x.NumeroConselho).HasColumnName("numero_conselho").HasMaxLength(30);
        builder.Property(x => x.UfConselho).HasColumnName("uf_conselho").HasMaxLength(2);
        builder.Property(x => x.GrafiasJson).HasColumnName("grafias_json").HasColumnType("jsonb");
        builder.Property(x => x.Origem).HasColumnName("origem").IsRequired();
        builder.Property(x => x.Ocorrencias).HasColumnName("ocorrencias").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        // As duas travas contra duplicação: o mesmo CPF e o mesmo nome normalizado, por sistema.
        builder.HasIndex(x => new { x.Sistema, x.Cpf })
            .IsUnique().HasFilter("cpf IS NOT NULL")
            .HasDatabaseName("ux_regulacao_medico_local_cpf");
        builder.HasIndex(x => new { x.Sistema, x.NomeNormalizado })
            .IsUnique()
            .HasDatabaseName("ux_regulacao_medico_local_nome");
    }
}
