using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class AgendamentoExternoConfiguration : IEntityTypeConfiguration<AgendamentoExterno>
{
    public void Configure(EntityTypeBuilder<AgendamentoExterno> builder)
    {
        builder.ToTable("agendamento_externo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Sistema).HasColumnName("sistema").HasConversion<int>().IsRequired();
        builder.Property(a => a.OrigemId).HasColumnName("origem_id").IsRequired();
        builder.Property(a => a.NumeroExterno).HasColumnName("numero_externo").HasMaxLength(40).IsRequired();
        builder.Property(a => a.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(a => a.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(a => a.Procedimento).HasColumnName("procedimento").HasMaxLength(400).IsRequired();
        builder.Property(a => a.Data).HasColumnName("data").IsRequired();
        builder.Property(a => a.Hora).HasColumnName("hora");
        builder.Property(a => a.LocalNome).HasColumnName("local_nome").HasMaxLength(300);
        builder.Property(a => a.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(a => a.EncerradoEm).HasColumnName("encerrado_em");
        builder.Property(a => a.StatusConfirmacao).HasColumnName("status_confirmacao").HasConversion<int>().IsRequired();
        builder.Property(a => a.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(a => a.ConfirmadoCanal).HasColumnName("confirmado_canal").HasMaxLength(40);
        builder.Property(a => a.ConfirmacaoCanceladaEm).HasColumnName("confirmacao_cancelada_em");
        builder.Property(a => a.MotivoCancelamentoPaciente).HasColumnName("motivo_cancelamento_paciente").HasMaxLength(500);
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");

        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Um agendamento VIGENTE por pedido do espelho: a data nova encerra a anterior.
        builder.HasIndex(a => new { a.Sistema, a.OrigemId })
            .IsUnique()
            .HasFilter("situacao = 1");
        builder.HasIndex(a => a.OrigemId);
        builder.HasIndex(a => a.PacienteId);
    }
}
