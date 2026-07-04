using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class SolicitacaoExameConfiguration : IEntityTypeConfiguration<SolicitacaoExame>
{
    public void Configure(EntityTypeBuilder<SolicitacaoExame> builder)
    {
        builder.ToTable("solicitacao_exame");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.AccessionNumber).HasColumnName("accession_number").HasMaxLength(16).IsRequired();
        builder.Property(s => s.StudyInstanceUID).HasColumnName("study_instance_uid").HasMaxLength(128).IsRequired();
        builder.Property(s => s.WorklistItemUid).HasColumnName("worklist_item_uid").HasMaxLength(128);

        builder.Property(s => s.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(s => s.TipoExameId).HasColumnName("tipo_exame_id").IsRequired();
        builder.Property(s => s.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(s => s.UnidadeSolicitanteId).HasColumnName("unidade_solicitante_id");

        builder.Property(s => s.SolicitanteUsuarioId).HasColumnName("solicitante_usuario_id");
        builder.Property(s => s.SolicitanteNome).HasColumnName("solicitante_nome").HasMaxLength(200).IsRequired();
        builder.Property(s => s.SolicitanteNumConselho).HasColumnName("solicitante_num_conselho").HasMaxLength(20).IsRequired();
        builder.Property(s => s.SolicitanteUfConselho).HasColumnName("solicitante_uf_conselho").HasMaxLength(2).IsRequired();
        builder.Property(s => s.SolicitanteCpf).HasColumnName("solicitante_cpf").HasMaxLength(11);
        // Linha crua do TXT do SISREG (proveniência da importação) — text, sem limite.
        builder.Property(s => s.RawSisreg).HasColumnName("raw_sisreg");
        // Default "CRM" também faz o backfill das linhas existentes (todas legado = médico).
        builder.Property(s => s.SolicitanteConselho).HasColumnName("solicitante_conselho").HasMaxLength(20).HasDefaultValue("CRM").IsRequired();

        builder.Property(s => s.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(50);
        builder.Property(s => s.ChaveConfirmacao).HasColumnName("chave_confirmacao").HasMaxLength(100);
        builder.Property(s => s.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);

        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(s => s.Prioridade).HasColumnName("prioridade").HasConversion<int>().IsRequired();
        builder.Property(s => s.Observacoes).HasColumnName("observacoes").HasMaxLength(2000);

        // Data da solicitação (dia de calendário, sem hora) — mapeada para "date".
        builder.Property(s => s.DataSolicitacao).HasColumnName("data_solicitacao");
        // Data da regulação (dia de calendário) — capturada para estatística.
        builder.Property(s => s.DataRegulacao).HasColumnName("data_regulacao");
        builder.Property(s => s.DataAgendada).HasColumnName("data_agendada");
        builder.Property(s => s.IniciadoEm).HasColumnName("iniciado_em");
        builder.Property(s => s.RealizadoEm).HasColumnName("realizado_em");
        // DataEstudo vem do DICOM (StudyDate/StudyTime) — wall-clock local (Kind=Unspecified).
        // Precisa de "timestamp without time zone" (mesmo padrão de DeclaracaoComparecimentoVerificacao.DataHoraExame);
        // o default do Npgsql (timestamptz) rejeita DateTime Unspecified na escrita.
        builder.Property(s => s.DataEstudo).HasColumnName("data_estudo").HasColumnType("timestamp without time zone");
        builder.Property(s => s.ErroIntegracaoPacs).HasColumnName("erro_integracao_pacs").HasMaxLength(1000);

        builder.Property(s => s.TentativasEnvio).HasColumnName("tentativas_envio").HasDefaultValue(0).IsRequired();
        builder.Property(s => s.UltimaTentativaEm).HasColumnName("ultima_tentativa_em");
        builder.Property(s => s.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");

        // Confirmação pelo paciente (notificação WhatsApp/app) — default Pendente backfilla o legado.
        builder.Property(s => s.StatusConfirmacao).HasColumnName("status_confirmacao").HasConversion<int>().HasDefaultValue(Entities.Enums.StatusConfirmacaoAgendamento.Pendente).IsRequired();
        builder.Property(s => s.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(s => s.ConfirmadoCanal).HasColumnName("confirmado_canal").HasMaxLength(30);
        builder.Property(s => s.ConfirmacaoCanceladaEm).HasColumnName("confirmacao_cancelada_em");
        builder.Property(s => s.MotivoCancelamentoPaciente).HasColumnName("motivo_cancelamento_paciente").HasMaxLength(500);

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
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.HasOne(s => s.TipoExame)
            .WithMany()
            .HasForeignKey(s => s.TipoExameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Unidade)
            .WithMany()
            .HasForeignKey(s => s.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.UnidadeSolicitante)
            .WithMany()
            .HasForeignKey(s => s.UnidadeSolicitanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SolicitanteUsuario)
            .WithMany()
            .HasForeignKey(s => s.SolicitanteUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.AccessionNumber).IsUnique();
        builder.HasIndex(s => s.StudyInstanceUID).IsUnique();

        // Trava de idempotência do número de regulação (SISREG). Único quando preenchido,
        // EXCETO o sentinela '0000' (extra-SUS, repetição intencional), nulos (manuais/PACS)
        // e linhas soft-deleted (uma solicitação excluída não deve "prender" o número do SISREG —
        // permite re-importar o mesmo código depois de excluir).
        builder.HasIndex(s => s.CodigoSolicitacao)
            .IsUnique()
            .HasFilter("codigo_solicitacao IS NOT NULL AND codigo_solicitacao <> '0000' AND excluido_em IS NULL");
        builder.HasIndex(s => s.PacienteId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.Status, s.DataAgendada }); // usado pelo SincronizadorExamesService

        // Usado pelo EnviadorWorklistService — pega Solicitada/Enviada com tentativa vencida.
        builder.HasIndex(s => new { s.Status, s.ProximaTentativaEm });
    }
}
