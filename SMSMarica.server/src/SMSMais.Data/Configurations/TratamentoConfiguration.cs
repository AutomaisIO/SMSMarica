using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class TratamentoConfiguration : IEntityTypeConfiguration<Tratamento>
{
    public void Configure(EntityTypeBuilder<Tratamento> builder)
    {
        builder.ToTable("tratamento");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(t => t.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(t => t.TipoTratamentoId).HasColumnName("tipo_tratamento_id");
        builder.Property(t => t.Descricao).HasColumnName("descricao").HasMaxLength(500).IsRequired();
        builder.Property(t => t.CodigoSusLiberacao).HasColumnName("codigo_sus_liberacao").HasMaxLength(60);
        builder.Property(t => t.Observacoes).HasColumnName("observacoes");
        builder.Property(t => t.HoraPrevistaBusca).HasColumnName("hora_prevista_busca");
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.EncerradoEm).HasColumnName("encerrado_em");

        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.HasOne(t => t.Unidade)
            .WithMany()
            .HasForeignKey(t => t.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TipoTratamento)
            .WithMany()
            .HasForeignKey(t => t.TipoTratamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Periodicidade)
            .WithOne(p => p.Tratamento!)
            .HasForeignKey<Periodicidade>(p => p.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Sessoes)
            .WithOne(s => s.Tratamento)
            .HasForeignKey(s => s.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.PacienteId);
        builder.HasIndex(t => t.Ativo);
    }
}
