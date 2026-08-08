using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

/// <summary>
/// Espinha de regulação (ADR-0021). Espelha as colunas de regulação que hoje moram em
/// <c>solicitacao_exame</c>; o satélite <c>exame_imagem</c> fica com as de execução.
/// </summary>
internal sealed class SolicitacaoConfiguration : IEntityTypeConfiguration<Solicitacao>
{
    public void Configure(EntityTypeBuilder<Solicitacao> builder)
    {
        builder.ToTable("solicitacao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(s => s.Categoria).HasColumnName("categoria").HasConversion<int>().IsRequired();

        builder.Property(s => s.ProcedimentoCodigoSisreg).HasColumnName("procedimento_codigo_sisreg").HasMaxLength(20);
        builder.Property(s => s.ProcedimentoSigtapCodigo).HasColumnName("procedimento_sigtap_codigo").HasMaxLength(20);
        builder.Property(s => s.ProcedimentoTexto).HasColumnName("procedimento_texto").HasMaxLength(300);
        builder.Property(s => s.EspecialidadeTexto).HasColumnName("especialidade_texto").HasMaxLength(200);

        builder.Property(s => s.UnidadeExecutanteId).HasColumnName("unidade_executante_id").IsRequired();
        builder.Property(s => s.UnidadeSolicitanteId).HasColumnName("unidade_solicitante_id");

        builder.Property(s => s.SolicitanteUsuarioId).HasColumnName("solicitante_usuario_id");
        builder.Property(s => s.SolicitanteNome).HasColumnName("solicitante_nome").HasMaxLength(200).IsRequired();
        builder.Property(s => s.SolicitanteNumConselho).HasColumnName("solicitante_num_conselho").HasMaxLength(20).IsRequired();
        builder.Property(s => s.SolicitanteUfConselho).HasColumnName("solicitante_uf_conselho").HasMaxLength(2).IsRequired();
        builder.Property(s => s.SolicitanteCpf).HasColumnName("solicitante_cpf").HasMaxLength(11);
        builder.Property(s => s.SolicitanteConselho).HasColumnName("solicitante_conselho").HasMaxLength(20).HasDefaultValue("CRM").IsRequired();

        builder.Property(s => s.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(50);
        builder.Property(s => s.ChaveConfirmacao).HasColumnName("chave_confirmacao").HasMaxLength(100);
        builder.Property(s => s.RawSisreg).HasColumnName("raw_sisreg");
        builder.Property(s => s.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);
        builder.Property(s => s.Observacoes).HasColumnName("observacoes").HasMaxLength(2000);

        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(s => s.Prioridade).HasColumnName("prioridade").HasConversion<int>().IsRequired();

        builder.Property(s => s.DataSolicitacao).HasColumnName("data_solicitacao");
        builder.Property(s => s.DataRegulacao).HasColumnName("data_regulacao");
        builder.Property(s => s.DataAgendada).HasColumnName("data_agendada");

        builder.Property(s => s.StatusConfirmacao).HasColumnName("status_confirmacao").HasConversion<int>()
            .HasDefaultValue(Entities.Enums.StatusConfirmacaoAgendamento.Pendente).IsRequired();
        builder.Property(s => s.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(s => s.ConfirmadoCanal).HasColumnName("confirmado_canal").HasMaxLength(30);
        builder.Property(s => s.ConfirmacaoCanceladaEm).HasColumnName("confirmacao_cancelada_em");
        builder.Property(s => s.MotivoCancelamentoPaciente).HasColumnName("motivo_cancelamento_paciente").HasMaxLength(500);

        builder.Property(s => s.AutorizadoEm).HasColumnName("autorizado_em");
        builder.Property(s => s.AutorizadoPor).HasColumnName("autorizado_por");

        builder.Property(s => s.CanceladoEm).HasColumnName("cancelado_em");
        builder.Property(s => s.CanceladoPorUsuarioId).HasColumnName("cancelado_por_usuario_id");
        builder.Property(s => s.MotivoCancelamento).HasColumnName("motivo_cancelamento").HasMaxLength(500);

        builder.Property(s => s.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(s => s.CriadoPor).HasColumnName("criado_por");
        builder.Property(s => s.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(s => s.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(s => s.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(s => s.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(s => s.RowVersion)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.HasOne(s => s.UnidadeExecutante)
            .WithMany().HasForeignKey(s => s.UnidadeExecutanteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.UnidadeSolicitante)
            .WithMany().HasForeignKey(s => s.UnidadeSolicitanteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.SolicitanteUsuario)
            .WithMany().HasForeignKey(s => s.SolicitanteUsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Satélite de execução (0..1). FK do lado ExameImagem (ver ExameImagemConfiguration).
        builder.HasOne(s => s.ExameImagem)
            .WithOne(e => e.Solicitacao)
            .HasForeignKey<ExameImagem>(e => e.SolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotência do nº SISREG — mesma régua da solicitacao_exame legada
        // ([[project_sisreg_solicitacao_idempotencia]]): único quando preenchido, exceto '0000',
        // nulos e soft-deleted.
        builder.HasIndex(s => s.CodigoSolicitacao)
            .IsUnique()
            .HasFilter("codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL");
        builder.HasIndex(s => s.PacienteId);
        builder.HasIndex(s => s.Categoria);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.Status, s.DataAgendada });
    }
}
