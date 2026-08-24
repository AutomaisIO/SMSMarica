using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Pep;

namespace SMSMais.Data.Configurations.Pep;

internal sealed class PepSincronizacaoEstadoConfiguration : IEntityTypeConfiguration<PepSincronizacaoEstado>
{
    public void Configure(EntityTypeBuilder<PepSincronizacaoEstado> builder)
    {
        builder.ToTable("pep_sincronizacao_estado");
        builder.HasKey(x => x.FonteId);

        builder.Property(x => x.FonteId).HasColumnName("fonte_id");
        builder.Property(x => x.UltimoSyncProfissionalEm).HasColumnName("ultimo_sync_medico_em");
        builder.Property(x => x.UltimoSyncPacienteEm).HasColumnName("ultimo_sync_paciente_em");
        builder.Property(x => x.UltimoSyncAtendimentoEm).HasColumnName("ultimo_sync_baa_em");
        builder.Property(x => x.UltimoSyncDocumentoEm).HasColumnName("ultimo_sync_edoc_em");
        builder.Property(x => x.UltimoSyncInternacaoEm).HasColumnName("ultimo_sync_fia_em");
        builder.Property(x => x.UltimoSyncLogDocumentoId).HasColumnName("ultimo_sync_edoc_log_id");
        builder.Property(x => x.PonteirosJson).HasColumnName("ponteiros_json").HasColumnType("jsonb");
        builder.Property(x => x.PacienteCursorCd).HasColumnName("paciente_cursor_cd");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
    }
}
