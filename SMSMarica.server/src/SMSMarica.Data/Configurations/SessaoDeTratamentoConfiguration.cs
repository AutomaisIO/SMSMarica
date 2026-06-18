using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class SessaoDeTratamentoConfiguration : IEntityTypeConfiguration<SessaoDeTratamento>
{
    public void Configure(EntityTypeBuilder<SessaoDeTratamento> builder)
    {
        builder.ToTable("sessao_de_tratamento");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TratamentoId).HasColumnName("tratamento_id").IsRequired();

        builder.Property(s => s.DataPrevista).HasColumnName("data_prevista").IsRequired();
        builder.Property(s => s.HoraPrevistaBusca).HasColumnName("hora_prevista_busca");
        builder.Property(s => s.HoraPrevistaRetorno).HasColumnName("hora_prevista_retorno");

        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();

        builder.Property(s => s.RealizadaEm).HasColumnName("realizada_em");
        builder.Property(s => s.ConfirmadaPorUsuarioId).HasColumnName("confirmada_por_usuario_id");
        builder.Property(s => s.NomeAcompanhante).HasColumnName("nome_acompanhante").HasMaxLength(200);
        builder.Property(s => s.ParentescoAcompanhante).HasColumnName("parentesco_acompanhante").HasMaxLength(60);
        builder.Property(s => s.AcompanhanteEsperado).HasColumnName("acompanhante_esperado");
        builder.Property(s => s.AcompanhanteConfirmadoEm).HasColumnName("acompanhante_confirmado_em");
        builder.Property(s => s.AcompanhanteCanal).HasColumnName("acompanhante_canal").HasConversion<int>();

        builder.Property(s => s.MotoristaIdaId).HasColumnName("motorista_ida_id");
        builder.Property(s => s.VeiculoIdaId).HasColumnName("veiculo_ida_id");
        builder.Property(s => s.HoraSaidaResidencia).HasColumnName("hora_saida_residencia");
        builder.Property(s => s.HoraChegadaUnidade).HasColumnName("hora_chegada_unidade");

        builder.Property(s => s.MotoristaVoltaId).HasColumnName("motorista_volta_id");
        builder.Property(s => s.VeiculoVoltaId).HasColumnName("veiculo_volta_id");
        builder.Property(s => s.HoraSaidaUnidade).HasColumnName("hora_saida_unidade");
        builder.Property(s => s.HoraChegadaResidencia).HasColumnName("hora_chegada_residencia");

        builder.Property(s => s.MotivoNaoRealizacao).HasColumnName("motivo_nao_realizacao").HasMaxLength(500);
        builder.Property(s => s.Observacoes).HasColumnName("observacoes");

        builder.Property(s => s.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(s => s.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(s => new { s.TratamentoId, s.DataPrevista });
        builder.HasIndex(s => s.DataPrevista);
        builder.HasIndex(s => s.Status);
    }
}
