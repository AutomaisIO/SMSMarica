using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Configurations;

internal sealed class TratamentoConfiguration : IEntityTypeConfiguration<Tratamento>
{
    public void Configure(EntityTypeBuilder<Tratamento> builder)
    {
        builder.ToTable("tratamento", t =>
        {
            t.HasCheckConstraint("ck_tratamento_dias_semana", "dias_semana_mascara BETWEEN 1 AND 127");
            t.HasCheckConstraint("ck_tratamento_quantidade_acompanhantes", "quantidade_acompanhantes BETWEEN 1 AND 2");
            // Contínuo não tem total; o modo N sempre tem.
            t.HasCheckConstraint("ck_tratamento_agenda",
                "(continuo AND quantidade_sessoes IS NULL) OR (NOT continuo AND quantidade_sessoes BETWEEN 1 AND 365)");
        });
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(t => t.UnidadeAtendimentoId).HasColumnName("unidade_atendimento_id").IsRequired();
        builder.Property(t => t.TipoTratamentoId).HasColumnName("tipo_tratamento_id");
        builder.Property(t => t.Descricao).HasColumnName("descricao").HasMaxLength(500).IsRequired();
        builder.Property(t => t.Observacoes).HasColumnName("observacoes");

        builder.Property(t => t.DataInicio).HasColumnName("data_inicio").IsRequired();
        builder.Property(t => t.DiasSemanaMascara).HasColumnName("dias_semana_mascara").IsRequired();
        builder.Property(t => t.QuantidadeSessoes).HasColumnName("quantidade_sessoes");
        builder.Property(t => t.Continuo).HasColumnName("continuo").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.SessoesGeradasAte).HasColumnName("sessoes_geradas_ate");

        // Sentinela = o próprio padrão: sem ela o EF avisa que o valor 0 (inexistente no enum) cairia
        // no default do banco. Com ela, "Independente" e o default dão o mesmo resultado.
        builder.Property(t => t.Mobilidade).HasColumnName("mobilidade").HasConversion<int>()
            .HasDefaultValue(MobilidadeTransporte.Independente)
            .HasSentinel(MobilidadeTransporte.Independente)
            .IsRequired();
        builder.Property(t => t.DificuldadeVeiculoAlto).HasColumnName("dificuldade_veiculo_alto").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.Isolamento).HasColumnName("isolamento").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.UsaOxigenio).HasColumnName("usa_oxigenio").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.NecessitaAjuda).HasColumnName("necessita_ajuda").HasDefaultValue(false).IsRequired();
        builder.Property(t => t.AjudaDescricao).HasColumnName("ajuda_descricao").HasMaxLength(500);

        builder.Property(t => t.QuantidadeAcompanhantes).HasColumnName("quantidade_acompanhantes").HasDefaultValue(1).IsRequired();
        builder.Property(t => t.SegundoAcompanhanteJustificativa).HasColumnName("segundo_acompanhante_justificativa").HasMaxLength(500);
        builder.Property(t => t.SegundoAcompanhanteLiberadoPor).HasColumnName("segundo_acompanhante_liberado_por");
        builder.Property(t => t.SegundoAcompanhanteLiberadoEm).HasColumnName("segundo_acompanhante_liberado_em");

        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.EncerradoEm).HasColumnName("encerrado_em");

        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.HasOne(t => t.UnidadeAtendimento)
            .WithMany()
            .HasForeignKey(t => t.UnidadeAtendimentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TipoTratamento)
            .WithMany()
            .HasForeignKey(t => t.TipoTratamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Sessoes)
            .WithOne(s => s.Tratamento)
            .HasForeignKey(s => s.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.PacienteId);
        builder.HasIndex(t => t.Ativo);
    }
}
