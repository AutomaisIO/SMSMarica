using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class PacienteConfiguration : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("paciente");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UsuarioId).HasColumnName("usuario_id").IsRequired();
        builder.Property(p => p.NomeSocial).HasColumnName("nome_social").HasMaxLength(200);
        builder.Property(p => p.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(p => p.EstadoCivil).HasColumnName("estado_civil").HasConversion<int>().IsRequired();
        builder.Property(p => p.RacaCor).HasColumnName("raca_cor").HasConversion<int>().IsRequired();
        builder.Property(p => p.Escolaridade).HasColumnName("escolaridade").HasConversion<int>().IsRequired();
        builder.Property(p => p.Ocupacao).HasColumnName("ocupacao").HasMaxLength(120);
        builder.Property(p => p.Naturalidade).HasColumnName("naturalidade").HasMaxLength(120);
        builder.Property(p => p.Nacionalidade).HasColumnName("nacionalidade").HasMaxLength(60).IsRequired();

        builder.Property(p => p.NomeDaMae).HasColumnName("nome_da_mae").HasMaxLength(200);
        builder.Property(p => p.NomeDoPai).HasColumnName("nome_do_pai").HasMaxLength(200);
        builder.Property(p => p.ResponsavelLegal).HasColumnName("responsavel_legal").HasMaxLength(200);

        builder.Property(p => p.TelefoneCelular).HasColumnName("telefone_celular").HasMaxLength(20);
        builder.Property(p => p.TelefoneResidencial).HasColumnName("telefone_residencial").HasMaxLength(20);

        builder.Property(p => p.AlturaCm).HasColumnName("altura_cm");
        builder.Property(p => p.PesoKg).HasColumnName("peso_kg").HasPrecision(5, 2);
        builder.Property(p => p.TipoSanguineo).HasColumnName("tipo_sanguineo").HasConversion<int>().IsRequired();
        builder.Property(p => p.FatorRh).HasColumnName("fator_rh").HasConversion<int>().IsRequired();

        builder.Property(p => p.Alergias).HasColumnName("alergias").HasColumnType("text[]").IsRequired();
        builder.Property(p => p.MedicamentosContinuos).HasColumnName("medicamentos_continuos").HasColumnType("text[]").IsRequired();
        builder.Property(p => p.Comorbidades).HasColumnName("comorbidades").HasColumnType("text[]").IsRequired();
        builder.Property(p => p.Deficiencias).HasColumnName("deficiencias").HasColumnType("text[]").IsRequired();
        builder.Property(p => p.PlanoSaude).HasColumnName("plano_saude").HasMaxLength(120);

        builder.Property(p => p.Observacoes).HasColumnName("observacoes");

        // Auditoria
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.CriadoPor).HasColumnName("criado_por");
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(p => p.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(p => p.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(p => p.ExcluidoPor).HasColumnName("excluido_por");

        builder.OwnsOne(p => p.GpsResidencia, gps =>
        {
            gps.Property(g => g.Latitude).HasColumnName("residencia_latitude");
            gps.Property(g => g.Longitude).HasColumnName("residencia_longitude");
        });

        builder.OwnsOne(p => p.ContatoEmergencia, c =>
        {
            c.Property(x => x.Nome).HasColumnName("contato_emergencia_nome").HasMaxLength(200);
            c.Property(x => x.Parentesco).HasColumnName("contato_emergencia_parentesco").HasMaxLength(60);
            c.Property(x => x.Telefone).HasColumnName("contato_emergencia_telefone").HasMaxLength(20);
        });

        builder.HasOne(p => p.Usuario)
            .WithOne(u => u.Paciente)
            .HasForeignKey<Paciente>(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.UsuarioId).IsUnique();
        builder.HasIndex(p => p.ExcluidoEm)
            .HasDatabaseName("ix_paciente_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
