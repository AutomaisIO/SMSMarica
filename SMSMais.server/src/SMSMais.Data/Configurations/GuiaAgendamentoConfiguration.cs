using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Configurations;

internal sealed class GuiaAgendamentoConfiguration : IEntityTypeConfiguration<GuiaAgendamento>
{
    public void Configure(EntityTypeBuilder<GuiaAgendamento> builder)
    {
        builder.ToTable("guia_agendamento", t => t.HasCheckConstraint(
            "ck_guia_agendamento_uma_ancora",
            "(agendamento_externo_id IS NOT NULL)::int + (solicitacao_id IS NOT NULL)::int = 1"));
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.Sistema).HasColumnName("sistema").HasConversion<int>().IsRequired();
        builder.Property(g => g.AgendamentoExternoId).HasColumnName("agendamento_externo_id");
        builder.Property(g => g.SolicitacaoId).HasColumnName("solicitacao_id");
        builder.Property(g => g.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(g => g.DataAtendimento).HasColumnName("data_atendimento").IsRequired();
        builder.Property(g => g.Situacao).HasColumnName("situacao").HasConversion<int>().IsRequired();
        builder.Property(g => g.DocumentoPacienteId).HasColumnName("documento_paciente_id");
        builder.Property(g => g.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(g => g.UltimaTentativaEm).HasColumnName("ultima_tentativa_em");
        builder.Property(g => g.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        builder.Property(g => g.UltimoErro).HasColumnName("ultimo_erro").HasMaxLength(500);
        builder.Property(g => g.ObtidaEm).HasColumnName("obtida_em");
        builder.Property(g => g.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(g => g.AtualizadoEm).HasColumnName("atualizado_em");

        builder.Property(g => g.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne<AgendamentoExterno>().WithMany()
            .HasForeignKey(g => g.AgendamentoExternoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Solicitacao>().WithMany()
            .HasForeignKey(g => g.SolicitacaoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentoPaciente>().WithMany()
            .HasForeignKey(g => g.DocumentoPacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Uma guia por agendamento — a fila nunca enfileira (nem emite) duas vezes o mesmo.
        builder.HasIndex(g => g.AgendamentoExternoId).IsUnique().HasFilter("agendamento_externo_id IS NOT NULL");
        builder.HasIndex(g => new { g.SolicitacaoId, g.DataAtendimento }).IsUnique().HasFilter("solicitacao_id IS NOT NULL");
        // A fila de cada sistema: pendentes, o atendimento mais perto primeiro.
        builder.HasIndex(g => new { g.Sistema, g.Situacao, g.DataAtendimento });
        builder.HasIndex(g => g.DocumentoPacienteId);
        builder.HasIndex(g => g.PacienteId);
    }
}

internal sealed class GuiaFilaConfiguracaoConfiguration : IEntityTypeConfiguration<GuiaFilaConfiguracao>
{
    public void Configure(EntityTypeBuilder<GuiaFilaConfiguracao> builder)
    {
        builder.ToTable("guia_fila_configuracao", t => t.HasCheckConstraint(
            "ck_guia_fila_configuracao_por_hora", "por_hora BETWEEN 1 AND 3600"));
        builder.HasKey(c => c.Sistema);

        builder.Property(c => c.Sistema).HasColumnName("sistema").HasConversion<int>().ValueGeneratedNever();
        builder.Property(c => c.Habilitada).HasColumnName("habilitada").IsRequired();
        builder.Property(c => c.PorHora).HasColumnName("por_hora").IsRequired();
        builder.Property(c => c.JanelaInicio).HasColumnName("janela_inicio").IsRequired();
        builder.Property(c => c.JanelaFim).HasColumnName("janela_fim").IsRequired();
        builder.Property(c => c.PausadaAte).HasColumnName("pausada_ate");
        builder.Property(c => c.MotivoPausa).HasColumnName("motivo_pausa").HasMaxLength(300);
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
