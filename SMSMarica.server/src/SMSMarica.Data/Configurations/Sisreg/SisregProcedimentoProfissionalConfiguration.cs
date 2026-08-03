using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data.Configurations.Sisreg;

internal sealed class SisregProcedimentoProfissionalConfiguration
    : IEntityTypeConfiguration<SisregProcedimentoProfissional>
{
    public void Configure(EntityTypeBuilder<SisregProcedimentoProfissional> builder)
    {
        builder.ToTable("sisreg_procedimento_profissional");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProfissionalId).HasColumnName("profissional_id").IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Habilitado).HasColumnName("habilitado").IsRequired();
        // Default TRUE: preserva o envio que já roda em produção nos procedimentos existentes.
        builder.Property(x => x.EnviarConfirmacao).HasColumnName("enviar_confirmacao")
            .IsRequired().HasDefaultValue(true);
        builder.Property(x => x.Grupo).HasColumnName("grupo").IsRequired();
        builder.Property(x => x.VistoEm).HasColumnName("visto_em").IsRequired();
        builder.Property(x => x.Ausente).HasColumnName("ausente").IsRequired();

        builder.HasIndex(x => new { x.ProfissionalId, x.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_sisreg_procedimento_profissional_codigo");
    }
}
