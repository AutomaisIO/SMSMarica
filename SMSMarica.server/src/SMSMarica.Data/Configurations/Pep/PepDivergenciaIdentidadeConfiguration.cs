using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Data.Configurations.Pep;

internal sealed class PepDivergenciaIdentidadeConfiguration : IEntityTypeConfiguration<PepDivergenciaIdentidade>
{
    public void Configure(EntityTypeBuilder<PepDivergenciaIdentidade> builder)
    {
        builder.ToTable("pep_sincronizacao_divergencia");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.FonteSlug).HasColumnName("fonte_slug").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CdPaciente).HasColumnName("cd_paciente").IsRequired();
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(x => x.ValorOrigem).HasColumnName("valor_origem").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ValorHub).HasColumnName("valor_hub").HasMaxLength(40).IsRequired();
        builder.Property(x => x.NomeOrigem).HasColumnName("nome_origem").HasMaxLength(200);
        builder.Property(x => x.NomeHub).HasColumnName("nome_hub").HasMaxLength(200);
        builder.Property(x => x.PatientIdHub).HasColumnName("patient_id_hub").HasMaxLength(64);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.Veredicto).HasColumnName("veredicto").HasConversion<int>().IsRequired();
        builder.Property(x => x.VeredictoMotor).HasColumnName("veredicto_motor").HasMaxLength(60);
        builder.Property(x => x.ValorCorreto).HasColumnName("valor_correto").HasMaxLength(40);
        builder.Property(x => x.NomeOficial).HasColumnName("nome_oficial").HasMaxLength(200);
        builder.Property(x => x.Detalhe).HasColumnName("detalhe").HasMaxLength(500);
        builder.Property(x => x.Ocorrencias).HasColumnName("ocorrencias").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(x => x.VerificadoEm).HasColumnName("verificado_em");
        builder.Property(x => x.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(x => x.ResolvidoPor).HasColumnName("resolvido_por");

        // Uma divergência viva por (base, CPF, tipo): re-detecção ATUALIZA (ocorrências++),
        // nunca duplica — senão um sincronismo de 30 min encheria a tabela com o mesmo conflito.
        builder.HasIndex(x => new { x.FonteId, x.Cpf, x.Tipo })
            .IsUnique()
            .HasDatabaseName("ux_pep_divergencia_fonte_cpf_tipo");

        // Fila de arbitragem: pendentes por base, mais antigas primeiro.
        builder.HasIndex(x => new { x.FonteId, x.Status })
            .HasDatabaseName("ix_pep_divergencia_fonte_status");

        // Carga do conjunto de CPFs congelados no início do run (poucas centenas).
        builder.HasIndex(x => new { x.FonteId, x.Cpf })
            .HasDatabaseName("ix_pep_divergencia_fonte_cpf");
    }
}
