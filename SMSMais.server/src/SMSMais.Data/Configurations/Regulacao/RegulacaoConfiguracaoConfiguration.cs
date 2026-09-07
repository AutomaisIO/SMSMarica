using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoConfiguracaoConfiguration
    : IEntityTypeConfiguration<RegulacaoConfiguracao>
{
    public void Configure(EntityTypeBuilder<RegulacaoConfiguracao> builder)
    {
        builder.ToTable("regulacao_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.PermitirExternoComInterno)
            .HasColumnName("permitir_externo_com_interno").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.PontaPodeEscolherUnidade)
            .HasColumnName("ponta_pode_escolher_unidade").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.PontaPodeVerTodasUnidades)
            .HasColumnName("ponta_pode_ver_todas_unidades").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.ExigirCpf)
            .HasColumnName("exigir_cpf").IsRequired().HasDefaultValue(true);
        builder.Property(c => c.RotuloFila)
            .HasColumnName("rotulo_fila").HasMaxLength(60).IsRequired();
        builder.Property(c => c.RascunhosLegadosMigradosEm)
            .HasColumnName("rascunhos_legados_migrados_em");

        builder.Property(c => c.SisregPrazoEdicaoDias)
            .HasColumnName("sisreg_prazo_edicao_dias").IsRequired().HasDefaultValue(7);

        // numeric(4,3): o corte vai de 0 a 1 com três casas — 0,45 e 0,85 são calibráveis em
        // milésimos, e `real` traria ruído de ponto flutuante numa comparação de fronteira.
        builder.Property(c => c.BuscaCorteDistancia)
            .HasColumnName("busca_corte_distancia").HasColumnType("numeric(4,3)").IsRequired();
        builder.Property(c => c.BuscaScoreSugestaoPareamento)
            .HasColumnName("busca_score_sugestao_pareamento").HasColumnType("numeric(4,3)").IsRequired();

        builder.Property(c => c.AnexoLimiteMb)
            .HasColumnName("anexo_limite_mb").IsRequired().HasDefaultValue(15);
        builder.Property(c => c.AnexoTiposPermitidos)
            .HasColumnName("anexo_tipos_permitidos").IsRequired();

        builder.Property(c => c.RegrasFollowupJson)
            .HasColumnName("regras_followup_json").HasColumnType("jsonb").IsRequired();

        builder.Property(c => c.NaoSeiPadrao).HasColumnName("nao_sei_padrao").IsRequired();

        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");

        builder.Property(c => c.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
