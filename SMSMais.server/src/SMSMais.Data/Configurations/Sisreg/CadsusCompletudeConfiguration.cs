using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class CadsusCompletudeConfiguration : IEntityTypeConfiguration<CadsusCompletude>
{
    public void Configure(EntityTypeBuilder<CadsusCompletude> builder)
    {
        builder.ToTable("cadsus_completude");

        // O CNS É a chave: uma memória por número, sem id sintético para desincronizar.
        builder.HasKey(x => x.Cns);

        builder.Property(x => x.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(x => x.ConsultadoEm).HasColumnName("consultado_em").IsRequired();
        builder.Property(x => x.Desfecho).HasColumnName("desfecho").IsRequired();
        builder.Property(x => x.PacienteDestinoId).HasColumnName("paciente_destino_id");
    }
}
