using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class IndicadorConfiguration : IEntityTypeConfiguration<Indicador>
{
    public void Configure(EntityTypeBuilder<Indicador> builder)
    {
        builder.ToTable("indicador");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.Aba).HasColumnName("aba").HasConversion<int>().IsRequired();
        builder.Property(i => i.Numero).HasColumnName("numero").HasMaxLength(10).IsRequired();
        builder.Property(i => i.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(i => i.IndicadorPaiId).HasColumnName("indicador_pai_id");
        builder.Property(i => i.Nome).HasColumnName("nome").HasMaxLength(400).IsRequired();
        builder.Property(i => i.MemoriaCalculo).HasColumnName("memoria_calculo");
        builder.Property(i => i.FonteDeclarada).HasColumnName("fonte_declarada").HasMaxLength(300);
        builder.Property(i => i.Meta).HasColumnName("meta").HasMaxLength(120);
        builder.Property(i => i.MetaOperador).HasColumnName("meta_operador").HasConversion<int?>();
        builder.Property(i => i.MetaValor).HasColumnName("meta_valor").HasPrecision(18, 4);
        builder.Property(i => i.MetaValorMaximo).HasColumnName("meta_valor_maximo").HasPrecision(18, 4);
        builder.Property(i => i.Pontuacao).HasColumnName("pontuacao").HasPrecision(6, 2);
        builder.Property(i => i.TipoResultado).HasColumnName("tipo_resultado").HasConversion<int>().IsRequired();
        builder.Property(i => i.UnidadeMedida).HasColumnName("unidade_medida").HasMaxLength(30);
        builder.Property(i => i.FatorDensidade).HasColumnName("fator_densidade").HasPrecision(12, 2);
        builder.Property(i => i.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(i => i.FonteId).HasColumnName("fonte_id");
        builder.Property(i => i.Sql).HasColumnName("sql");
        builder.Property(i => i.SqlAnalitico).HasColumnName("sql_analitico");
        builder.Property(i => i.Ressalva).HasColumnName("ressalva");
        builder.Property(i => i.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(i => i.CriadoPor).HasColumnName("criado_por");
        builder.Property(i => i.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(i => i.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(i => i.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(i => i.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(i => i.Fonte)
            .WithMany()
            .HasForeignKey(i => i.FonteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Indicador>()
            .WithMany()
            .HasForeignKey(i => i.IndicadorPaiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Versoes)
            .WithOne(v => v.Indicador!)
            .HasForeignKey(v => v.IndicadorId)
            .OnDelete(DeleteBehavior.Cascade);

        // A planilha é o contrato: número é único dentro da aba enquanto o indicador existir.
        builder.HasIndex(i => new { i.Aba, i.Numero })
            .HasDatabaseName("ux_indicador_aba_numero")
            .IsUnique()
            .HasFilter("excluido_em IS NULL");

        builder.HasIndex(i => i.ExcluidoEm)
            .HasDatabaseName("ix_indicador_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}

internal sealed class IndicadorVersaoConfiguration : IEntityTypeConfiguration<IndicadorVersao>
{
    public void Configure(EntityTypeBuilder<IndicadorVersao> builder)
    {
        builder.ToTable("indicador_versao");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.IndicadorId).HasColumnName("indicador_id").IsRequired();
        builder.Property(v => v.Numero).HasColumnName("numero").IsRequired();
        builder.Property(v => v.Sql).HasColumnName("sql").IsRequired();
        builder.Property(v => v.Nota).HasColumnName("nota");
        builder.Property(v => v.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(v => v.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(v => new { v.IndicadorId, v.Numero })
            .HasDatabaseName("ux_indicador_versao_num")
            .IsUnique();
    }
}

internal sealed class IndicadorExecucaoConfiguration : IEntityTypeConfiguration<IndicadorExecucao>
{
    public void Configure(EntityTypeBuilder<IndicadorExecucao> builder)
    {
        builder.ToTable("indicador_execucao");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.IndicadorId).HasColumnName("indicador_id").IsRequired();
        builder.Property(e => e.IndicadorVersaoId).HasColumnName("indicador_versao_id");
        builder.Property(e => e.Hospital).HasColumnName("hospital").IsRequired();
        builder.Property(e => e.PeriodoInicio).HasColumnName("periodo_inicio").IsRequired();
        builder.Property(e => e.PeriodoFim).HasColumnName("periodo_fim").IsRequired();
        builder.Property(e => e.Numerador).HasColumnName("numerador").HasPrecision(18, 4);
        builder.Property(e => e.Denominador).HasColumnName("denominador").HasPrecision(18, 4);
        builder.Property(e => e.Valor).HasColumnName("valor").HasPrecision(18, 4);
        builder.Property(e => e.DistribuicaoJson).HasColumnName("distribuicao_json").HasColumnType("jsonb");
        builder.Property(e => e.AtingiuMeta).HasColumnName("atingiu_meta");
        builder.Property(e => e.PontuacaoApurada).HasColumnName("pontuacao_apurada").HasPrecision(6, 2);
        builder.Property(e => e.DuracaoMs).HasColumnName("duracao_ms").IsRequired();
        builder.Property(e => e.Erro).HasColumnName("erro");
        builder.Property(e => e.ExecutadoEm).HasColumnName("executado_em").IsRequired();
        builder.Property(e => e.ExecutadoPor).HasColumnName("executado_por");

        builder.HasOne(e => e.Indicador)
            .WithMany()
            .HasForeignKey(e => e.IndicadorId)
            .OnDelete(DeleteBehavior.Cascade);

        // A tela abre sempre por indicador + período: índice cobre a leitura do último resultado.
        builder.HasIndex(e => new { e.IndicadorId, e.Hospital, e.PeriodoInicio, e.PeriodoFim })
            .HasDatabaseName("ix_indicador_execucao_periodo");
    }
}
