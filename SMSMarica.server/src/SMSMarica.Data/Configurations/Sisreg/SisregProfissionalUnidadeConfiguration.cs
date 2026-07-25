using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data.Configurations.Sisreg;

internal sealed class SisregProfissionalUnidadeConfiguration : IEntityTypeConfiguration<SisregProfissionalUnidade>
{
    public void Configure(EntityTypeBuilder<SisregProfissionalUnidade> builder)
    {
        builder.ToTable("sisreg_profissional_unidade");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Habilitado).HasColumnName("habilitado").IsRequired();
        builder.Property(x => x.PractitionerId).HasColumnName("practitioner_id");
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em");
        builder.Property(x => x.VistoEm).HasColumnName("visto_em").IsRequired();
        builder.Property(x => x.Ausente).HasColumnName("ausente").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        // O mesmo CPF pode atender em mais de uma unidade — a chave natural é o par.
        builder.HasIndex(x => new { x.UnidadeId, x.Cpf })
            .IsUnique()
            .HasDatabaseName("ux_sisreg_profissional_unidade_cpf");

        // Listagem da tela e seleção da varredura (só habilitados).
        builder.HasIndex(x => new { x.UnidadeId, x.Habilitado })
            .HasDatabaseName("ix_sisreg_profissional_unidade_habilitado");

        builder.HasOne<Entities.Unidade>()
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Procedimentos)
            .WithOne(x => x.Profissional!)
            .HasForeignKey(x => x.ProfissionalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
