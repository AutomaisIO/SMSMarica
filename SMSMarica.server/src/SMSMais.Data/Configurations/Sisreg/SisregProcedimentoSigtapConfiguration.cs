using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregProcedimentoSigtapConfiguration : IEntityTypeConfiguration<SisregProcedimentoSigtap>
{
    public void Configure(EntityTypeBuilder<SisregProcedimentoSigtap> builder)
    {
        builder.ToTable("sisreg_procedimento_sigtap");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Grupo).HasColumnName("grupo").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ProcedimentoSigtapId).HasColumnName("procedimento_sigtap_id");
        builder.Property(x => x.CodigoSigtap).HasColumnName("codigo_sigtap").HasMaxLength(10);
        builder.Property(x => x.SugeridoSigtapId).HasColumnName("sugerido_sigtap_id");
        builder.Property(x => x.SugeridoScore).HasColumnName("sugerido_score");
        builder.Property(x => x.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(x => x.ConfirmadoPor).HasColumnName("confirmado_por");
        builder.Property(x => x.PrimeiroVistoEm).HasColumnName("primeiro_visto_em").IsRequired();
        builder.Property(x => x.VistoEm).HasColumnName("visto_em").IsRequired();

        // O `pa` é a chave natural do catálogo — é o que impede o mesmo procedimento do SISREG
        // de acabar mapeado para dois SIGTAPs diferentes.
        builder.HasIndex(x => x.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_sisreg_procedimento_sigtap_codigo");

        // A tela de de-para abre filtrando "o que falta confirmar".
        builder.HasIndex(x => x.ConfirmadoEm)
            .HasDatabaseName("ix_sisreg_procedimento_sigtap_confirmado");

        builder.HasOne(x => x.ProcedimentoSigtap)
            .WithMany()
            .HasForeignKey(x => x.ProcedimentoSigtapId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
