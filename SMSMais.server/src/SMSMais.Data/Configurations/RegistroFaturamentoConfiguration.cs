using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Tfd;

namespace SMSMais.Data.Configurations;

internal sealed class RegistroFaturamentoConfiguration : IEntityTypeConfiguration<RegistroFaturamento>
{
    public void Configure(EntityTypeBuilder<RegistroFaturamento> builder)
    {
        builder.ToTable("tfd_registro_faturamento");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.SessaoId).HasColumnName("sessao_id").IsRequired();
        builder.Property(r => r.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(r => r.MotoristaId).HasColumnName("motorista_id");
        builder.Property(r => r.VeiculoId).HasColumnName("veiculo_id");
        builder.Property(r => r.TipoTratamentoId).HasColumnName("tipo_tratamento_id");
        builder.Property(r => r.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(r => r.Competencia).HasColumnName("competencia").IsRequired();
        builder.Property(r => r.Data).HasColumnName("data").IsRequired();
        builder.Property(r => r.KmComPaciente).HasColumnName("km_com_paciente").HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(r => r.Unidades).HasColumnName("unidades").HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(r => r.ValorUnitario).HasColumnName("valor_unitario").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(r => r.ValorTotal).HasColumnName("valor_total").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(r => r.CodigoSigtap).HasColumnName("codigo_sigtap").HasMaxLength(20);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(r => r.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(r => r.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasOne(r => r.Sessao).WithMany().HasForeignKey(r => r.SessaoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.SessaoId).IsUnique();
        builder.HasIndex(r => r.Competencia);
    }
}
