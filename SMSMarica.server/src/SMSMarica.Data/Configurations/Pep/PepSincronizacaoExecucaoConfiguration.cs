using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Data.Configurations.Pep;

internal sealed class PepSincronizacaoExecucaoConfiguration : IEntityTypeConfiguration<PepSincronizacaoExecucao>
{
    public void Configure(EntityTypeBuilder<PepSincronizacaoExecucao> builder)
    {
        builder.ToTable("pep_sincronizacao_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FonteId).HasColumnName("fonte_id").IsRequired();
        builder.Property(x => x.FonteNome).HasColumnName("fonte_nome").HasMaxLength(200).IsRequired();

        builder.Property(x => x.Modo).HasColumnName("modo").HasConversion<int>().IsRequired();
        builder.Property(x => x.Escopo).HasColumnName("escopo").HasConversion<int>().IsRequired();
        builder.Property(x => x.ApagarAntes).HasColumnName("apagar_antes").IsRequired();
        builder.Property(x => x.MaxMedicos).HasColumnName("max_medicos");
        builder.Property(x => x.MaxPacientes).HasColumnName("max_pacientes");

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.Disparo).HasColumnName("disparo").HasConversion<int>().IsRequired()
            .HasDefaultValue(Entities.Enums.DisparoSincronizacao.Manual);

        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");

        builder.Property(x => x.Medicos).HasColumnName("medicos").IsRequired();
        builder.Property(x => x.Pacientes).HasColumnName("pacientes").IsRequired();
        builder.Property(x => x.Encounters).HasColumnName("encounters").IsRequired();
        builder.Property(x => x.Conditions).HasColumnName("conditions").IsRequired();
        builder.Property(x => x.MedicationRequests).HasColumnName("medication_requests").IsRequired();
        builder.Property(x => x.DocumentReferences).HasColumnName("document_references").IsRequired();
        builder.Property(x => x.Observations).HasColumnName("observations").IsRequired();
        builder.Property(x => x.Falhas).HasColumnName("falhas").IsRequired();
        builder.Property(x => x.PacientesInalterados).HasColumnName("pacientes_inalterados").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.MedicosInalterados).HasColumnName("medicos_inalterados").IsRequired().HasDefaultValue(0);

        builder.Property(x => x.TemposJson).HasColumnName("tempos_json");
        builder.Property(x => x.FalhasJson).HasColumnName("falhas_json");
        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(x => new { x.FonteId, x.IniciadoEm }).HasDatabaseName("ix_pep_execucao_fonte_iniciado");
    }
}
