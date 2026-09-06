using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoFormularioVersaoConfiguration
    : IEntityTypeConfiguration<RegulacaoFormularioVersao>
{
    public void Configure(EntityTypeBuilder<RegulacaoFormularioVersao> builder)
    {
        builder.ToTable("regulacao_formulario_versao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Esquema).HasColumnName("esquema").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProcedimentoId).HasColumnName("procedimento_id").IsRequired();
        builder.Property(x => x.DefinicaoJson)
            .HasColumnName("definicao_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasOne(x => x.Procedimento).WithMany()
            .HasForeignKey(x => x.ProcedimentoId).OnDelete(DeleteBehavior.Cascade);

        // O hash É a identidade: dois procedimentos que geram a mesma união compartilham a linha
        // em vez de duplicá-la. Sem o unique, cada sincronismo criaria uma versão nova idêntica.
        builder.HasIndex(x => x.Hash).IsUnique().HasDatabaseName("ux_regulacao_form_versao_hash");
        builder.HasIndex(x => x.ProcedimentoId).HasDatabaseName("ix_regulacao_form_versao_procedimento");

        builder.HasMany(x => x.Mapa).WithOne(m => m.FormularioVersao)
            .HasForeignKey(m => m.FormularioVersaoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RegulacaoFormularioCampoMapaConfiguration
    : IEntityTypeConfiguration<RegulacaoFormularioCampoMapa>
{
    public void Configure(EntityTypeBuilder<RegulacaoFormularioCampoMapa> builder)
    {
        builder.ToTable("regulacao_formulario_campo_mapa");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FormularioVersaoId).HasColumnName("formulario_versao_id").IsRequired();
        builder.Property(x => x.ChaveCanonica).HasColumnName("chave_canonica").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.NomeNativo).HasColumnName("nome_nativo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Transformacao).HasColumnName("transformacao").HasMaxLength(2000);

        builder.HasIndex(x => new { x.FormularioVersaoId, x.ChaveCanonica, x.Sistema })
            .IsUnique().HasDatabaseName("ux_regulacao_form_mapa");
    }
}
