using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboConfiguracaoConfiguration : IEntityTypeConfiguration<RoboConfiguracao>
{
    public void Configure(EntityTypeBuilder<RoboConfiguracao> builder)
    {
        builder.ToTable("robo_configuracao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.PersonaGlobal).HasColumnName("persona_global").HasColumnType("text").IsRequired();
        builder.Property(x => x.ModeloPadrao).HasColumnName("modelo_padrao").HasMaxLength(100).IsRequired();
        builder.Property(x => x.NomeExibicao).HasColumnName("nome_exibicao").HasMaxLength(80).IsRequired();
        builder.Property(x => x.MensagemHandOff).HasColumnName("mensagem_handoff").HasMaxLength(1000);
        builder.Property(x => x.MensagemForaHorario).HasColumnName("mensagem_fora_horario").HasMaxLength(1000);
        builder.Property(x => x.HoraAtendimentoHumanoInicio).HasColumnName("hora_atendimento_humano_inicio");
        builder.Property(x => x.HoraAtendimentoHumanoFim).HasColumnName("hora_atendimento_humano_fim");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
