using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Agendamentos;

namespace SMSMais.Data.Configurations.Agendamentos;

internal sealed class BloqueioAgendaConfiguration : IEntityTypeConfiguration<BloqueioAgenda>
{
    public void Configure(EntityTypeBuilder<BloqueioAgenda> builder)
    {
        builder.ToTable("bloqueio_agenda");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.AgendaId).HasColumnName("agenda_id").IsRequired();
        builder.Property(b => b.InicioEm).HasColumnName("inicio_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(b => b.FimEm).HasColumnName("fim_em").HasColumnType("timestamp without time zone").IsRequired();
        builder.Property(b => b.Motivo).HasColumnName("motivo").HasMaxLength(300).IsRequired();
        builder.Property(b => b.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(b => b.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(b => b.AgendaId).HasDatabaseName("ix_bloqueio_agenda_agenda_id");
    }
}
