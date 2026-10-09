using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class PersonificacaoPacienteConfiguration : IEntityTypeConfiguration<PersonificacaoPaciente>
{
    public void Configure(EntityTypeBuilder<PersonificacaoPaciente> builder)
    {
        builder.ToTable("personificacao_paciente");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(p => p.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(p => p.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(p => p.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(p => p.EncerradaEm).HasColumnName("encerrada_em");

        builder.HasOne(p => p.Usuario)
            .WithMany()
            .HasForeignKey(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // No máximo UMA em aberto por operador — é ela que o login pelo CPF dele procura.
        builder.HasIndex(p => p.UsuarioId)
            .IsUnique()
            .HasFilter("encerrada_em IS NULL")
            .HasDatabaseName("ux_personificacao_paciente_usuario_aberta");
    }
}
