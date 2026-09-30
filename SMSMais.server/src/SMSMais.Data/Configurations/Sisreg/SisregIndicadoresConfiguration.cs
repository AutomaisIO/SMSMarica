using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregFaltaOficialConfiguration : IEntityTypeConfiguration<SisregFaltaOficial>
{
    public void Configure(EntityTypeBuilder<SisregFaltaOficial> builder)
    {
        builder.ToTable("sisreg_falta");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(20).IsRequired();
        builder.Property(x => x.DataExecucao).HasColumnName("data_execucao").IsRequired();
        builder.Property(x => x.Hora).HasColumnName("hora").HasMaxLength(10);
        builder.Property(x => x.Procedimento).HasColumnName("procedimento").HasMaxLength(300);
        builder.Property(x => x.UnidadeSolicitante).HasColumnName("unidade_solicitante").HasMaxLength(200);
        builder.Property(x => x.LidoEm).HasColumnName("lido_em").IsRequired();

        // O mesmo código pode faltar em datas diferentes (remarcado e faltou de novo).
        builder.HasIndex(x => new { x.CodigoSolicitacao, x.DataExecucao }).IsUnique()
            .HasDatabaseName("ix_sisreg_falta_codigo_data");
        builder.HasIndex(x => x.DataExecucao).HasDatabaseName("ix_sisreg_falta_data");
    }
}

internal sealed class SisregMarcacaoCanceladaConfiguration : IEntityTypeConfiguration<SisregMarcacaoCancelada>
{
    public void Configure(EntityTypeBuilder<SisregMarcacaoCancelada> builder)
    {
        builder.ToTable("sisreg_marcacao_cancelada");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(20).IsRequired();
        builder.Property(x => x.CanceladoEm).HasColumnName("cancelado_em").IsRequired();
        builder.Property(x => x.DataMarcacao).HasColumnName("data_marcacao");
        builder.Property(x => x.Procedimento).HasColumnName("procedimento").HasMaxLength(300);
        builder.Property(x => x.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);
        builder.Property(x => x.LidoEm).HasColumnName("lido_em").IsRequired();

        builder.HasIndex(x => new { x.CodigoSolicitacao, x.CanceladoEm }).IsUnique()
            .HasDatabaseName("ix_sisreg_marcacao_cancelada_codigo_instante");
        builder.HasIndex(x => x.CanceladoEm).HasDatabaseName("ix_sisreg_marcacao_cancelada_instante");
    }
}

internal sealed class SisregSolicitacaoDesfechoConfiguration : IEntityTypeConfiguration<SisregSolicitacaoDesfecho>
{
    public void Configure(EntityTypeBuilder<SisregSolicitacaoDesfecho> builder)
    {
        builder.ToTable("sisreg_solicitacao_desfecho");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(x => x.DataSolicitacao).HasColumnName("data_solicitacao");
        builder.Property(x => x.DataDesfecho).HasColumnName("data_desfecho");
        builder.Property(x => x.Procedimento).HasColumnName("procedimento").HasMaxLength(300);
        builder.Property(x => x.UnidadeSolicitanteCnes).HasColumnName("unidade_solicitante_cnes").HasMaxLength(10);
        builder.Property(x => x.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);
        builder.Property(x => x.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(x => x.LidoEm).HasColumnName("lido_em").IsRequired();

        builder.HasIndex(x => new { x.CodigoSolicitacao, x.Situacao }).IsUnique()
            .HasDatabaseName("ix_sisreg_solicitacao_desfecho_codigo_situacao");
        builder.HasIndex(x => x.DataSolicitacao).HasDatabaseName("ix_sisreg_solicitacao_desfecho_data_solicitacao");
        builder.HasIndex(x => x.DataDesfecho).HasDatabaseName("ix_sisreg_solicitacao_desfecho_data_desfecho");
    }
}

internal sealed class SisregPpiCotaConfiguration : IEntityTypeConfiguration<SisregPpiCota>
{
    public void Configure(EntityTypeBuilder<SisregPpiCota> builder)
    {
        builder.ToTable("sisreg_ppi_cota");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Competencia).HasColumnName("competencia").IsRequired();
        builder.Property(x => x.CodigoInterno).HasColumnName("codigo_interno").HasMaxLength(20).IsRequired();
        builder.Property(x => x.CodigoUnificado).HasColumnName("codigo_unificado").HasMaxLength(20);
        builder.Property(x => x.Procedimento).HasColumnName("procedimento").HasMaxLength(300);
        builder.Property(x => x.Total).HasColumnName("total").IsRequired();
        builder.Property(x => x.Usada).HasColumnName("usada").IsRequired();
        builder.Property(x => x.Saldo).HasColumnName("saldo");
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(30).IsRequired();
        builder.Property(x => x.LidoEm).HasColumnName("lido_em").IsRequired();

        builder.HasIndex(x => new { x.Competencia, x.CodigoInterno, x.Tipo }).IsUnique()
            .HasDatabaseName("ix_sisreg_ppi_cota_competencia_procedimento");
    }
}

internal sealed class SisregIndicadorColetaConfiguration : IEntityTypeConfiguration<SisregIndicadorColeta>
{
    public void Configure(EntityTypeBuilder<SisregIndicadorColeta> builder)
    {
        builder.ToTable("sisreg_indicador_coleta");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Coletor).HasColumnName("coletor").HasConversion<int>().IsRequired();
        builder.Property(x => x.JanelaInicio).HasColumnName("janela_inicio").IsRequired();
        builder.Property(x => x.JanelaFim).HasColumnName("janela_fim").IsRequired();
        builder.Property(x => x.Escopo).HasColumnName("escopo").HasMaxLength(20).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.Linhas).HasColumnName("linhas");
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em");
        builder.Property(x => x.LidoEm).HasColumnName("lido_em");
        builder.Property(x => x.Erro).HasColumnName("erro").HasMaxLength(1000);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(x => new { x.Coletor, x.JanelaInicio, x.Escopo }).IsUnique()
            .HasDatabaseName("ix_sisreg_indicador_coleta_item");
        builder.HasIndex(x => x.Status).HasFilter("status in (1, 2, 4)")
            .HasDatabaseName("ix_sisreg_indicador_coleta_a_fazer");
    }
}
