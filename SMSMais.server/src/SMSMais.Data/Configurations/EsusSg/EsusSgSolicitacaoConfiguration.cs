using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Data.Configurations.EsusSg;

internal sealed class EsusSgSolicitacaoConfiguration : IEntityTypeConfiguration<EsusSgSolicitacao>
{
    public void Configure(EntityTypeBuilder<EsusSgSolicitacao> builder)
    {
        builder.ToTable("esussg_solicitacao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdEsusSg).HasColumnName("id_esussg").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();

        builder.Property(x => x.Recurso).HasColumnName("recurso").HasMaxLength(300).IsRequired();
        builder.Property(x => x.CodigoInterno).HasColumnName("codigo_interno").HasMaxLength(20);
        builder.Property(x => x.Subprocedimentos).HasColumnName("subprocedimentos");
        builder.Property(x => x.DataSolicitacao).HasColumnName("data_solicitacao");
        builder.Property(x => x.DataEntradaFila).HasColumnName("data_entrada_fila");
        builder.Property(x => x.Prioridade).HasColumnName("prioridade").HasMaxLength(80);
        builder.Property(x => x.PrioridadeCor).HasColumnName("prioridade_cor").HasMaxLength(20);
        builder.Property(x => x.Pendencia).HasColumnName("pendencia").HasMaxLength(300);
        builder.Property(x => x.PosicaoFila).HasColumnName("posicao_fila");
        builder.Property(x => x.OrdemEntrada).HasColumnName("ordem_entrada");
        builder.Property(x => x.ProfissionalSolicitante).HasColumnName("profissional_solicitante").HasMaxLength(200);
        builder.Property(x => x.UnidadeSolicitante).HasColumnName("unidade_solicitante").HasMaxLength(200);
        builder.Property(x => x.UsuarioInclusao).HasColumnName("usuario_inclusao").HasMaxLength(200);
        builder.Property(x => x.Regulador).HasColumnName("regulador").HasMaxLength(200);

        builder.Property(x => x.PessoaIdEsus).HasColumnName("pessoa_id_esus").HasMaxLength(20);
        builder.Property(x => x.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(x => x.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(x => x.DataNascimento).HasColumnName("data_nascimento");
        builder.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(10);
        builder.Property(x => x.NomeMae).HasColumnName("nome_mae").HasMaxLength(200);
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(40);
        builder.Property(x => x.Celular).HasColumnName("celular").HasMaxLength(40);
        builder.Property(x => x.MunicipioPaciente).HasColumnName("municipio_paciente").HasMaxLength(120);
        builder.Property(x => x.Bairro).HasColumnName("bairro").HasMaxLength(150);

        builder.Property(x => x.UnidadeExecutora).HasColumnName("unidade_executora").HasMaxLength(300);
        builder.Property(x => x.CnesExecutora).HasColumnName("cnes_executora").HasMaxLength(7);
        builder.Property(x => x.Setor).HasColumnName("setor").HasMaxLength(200);
        builder.Property(x => x.Local).HasColumnName("local").HasMaxLength(200);
        builder.Property(x => x.DataAgendada).HasColumnName("data_agendada");
        builder.Property(x => x.DataHoraAgendadaTexto).HasColumnName("data_hora_agendada_texto").HasMaxLength(40);
        builder.Property(x => x.UsuarioAgendamento).HasColumnName("usuario_agendamento").HasMaxLength(200);
        builder.Property(x => x.AgendamentoCadastradoEm).HasColumnName("agendamento_cadastrado_em");
        builder.Property(x => x.DataSaidaFila).HasColumnName("data_saida_fila");
        builder.Property(x => x.ComprovanteImpresso).HasColumnName("comprovante_impresso");
        builder.Property(x => x.AgendadoTfd).HasColumnName("agendado_tfd");
        builder.Property(x => x.NotificacaoTipo).HasColumnName("notificacao_tipo").HasMaxLength(40);
        builder.Property(x => x.NotificacaoEntrega).HasColumnName("notificacao_entrega").HasMaxLength(40);
        builder.Property(x => x.NotificacaoResposta).HasColumnName("notificacao_resposta").HasMaxLength(60);

        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();

        builder.Property(x => x.PacienteId).HasColumnName("paciente_id");
        builder.Property(x => x.PacienteConciliarEm).HasColumnName("paciente_conciliar_em");
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();
        builder.Property(x => x.VistoNaFilaEm).HasColumnName("visto_na_fila_em");
        builder.Property(x => x.VistoNosAgendadosEm).HasColumnName("visto_nos_agendados_em");
        builder.Property(x => x.EventosCount).HasColumnName("eventos_count").IsRequired();
        builder.Property(x => x.UltimoEventoEm).HasColumnName("ultimo_evento_em");
        builder.Property(x => x.SituacaoMudouEm).HasColumnName("situacao_mudou_em");
        builder.Property(x => x.SituacaoAnterior).HasColumnName("situacao_anterior");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        // Chave natural (tipo, fil_id) — torna a re-varredura idempotente. O fil_id é o mesmo na
        // fila e nos agendados; o tipo entra porque consulta e exame são módulos distintos no ESUS.
        builder.HasIndex(x => new { x.Tipo, x.IdEsusSg })
            .IsUnique()
            .HasDatabaseName("ux_esussg_solicitacao_tipo_id")
            .HasFilter("excluido_em IS NULL");

        builder.HasIndex(x => new { x.Situacao, x.DataEntradaFila })
            .HasDatabaseName("ix_esussg_solicitacao_situacao_entrada");
        builder.HasIndex(x => x.DataAgendada).HasDatabaseName("ix_esussg_solicitacao_data_agendada");
        builder.HasIndex(x => x.Cpf).HasDatabaseName("ix_esussg_solicitacao_cpf");
        builder.HasIndex(x => x.Cns).HasDatabaseName("ix_esussg_solicitacao_cns");

        builder.HasIndex(x => x.PacienteId)
            .HasFilter("paciente_id IS NOT NULL AND excluido_em IS NULL")
            .HasDatabaseName("ix_esussg_solicitacao_paciente_id");

        builder.HasIndex(x => x.PacienteConciliarEm)
            .HasFilter("paciente_conciliar_em IS NOT NULL")
            .HasDatabaseName("ix_esussg_solicitacao_paciente_conciliar");

        builder.HasMany(x => x.Eventos)
            .WithOne(e => e.EsusSgSolicitacao!)
            .HasForeignKey(e => e.EsusSgSolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EsusSgEventoConfiguration : IEntityTypeConfiguration<EsusSgEvento>
{
    public void Configure(EntityTypeBuilder<EsusSgEvento> builder)
    {
        builder.ToTable("esussg_evento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EsusSgSolicitacaoId).HasColumnName("esussg_solicitacao_id").IsRequired();
        builder.Property(x => x.DataEvento).HasColumnName("data_evento").IsRequired();
        builder.Property(x => x.Evento).HasColumnName("evento").HasMaxLength(60).IsRequired();
        builder.Property(x => x.TipoEvento).HasColumnName("tipo_evento").IsRequired();
        builder.Property(x => x.FollowUpCategoria).HasColumnName("followup_categoria").HasMaxLength(40);
        builder.Property(x => x.FollowUpRegrasHash).HasColumnName("followup_regras_hash").HasMaxLength(16);
        builder.Property(x => x.EstadoAnterior).HasColumnName("estado_anterior").HasMaxLength(60);
        builder.Property(x => x.EstadoAtual).HasColumnName("estado_atual").HasMaxLength(60);
        builder.Property(x => x.CentralRegulacao).HasColumnName("central_regulacao").HasMaxLength(150);
        builder.Property(x => x.UnidadeExecutora).HasColumnName("unidade_executora").HasMaxLength(300);
        builder.Property(x => x.Usuario).HasColumnName("usuario").HasMaxLength(200);
        builder.Property(x => x.LotacaoEvento).HasColumnName("lotacao_evento").HasMaxLength(200);
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(45);
        builder.Property(x => x.Observacao).HasColumnName("observacao");
        builder.Property(x => x.CapturadoEm).HasColumnName("capturado_em").IsRequired();

        builder.HasIndex(x => new { x.EsusSgSolicitacaoId, x.DataEvento, x.Evento })
            .IsUnique()
            .HasDatabaseName("ux_esussg_evento_solicitacao_data_evento");

        builder.HasIndex(x => new { x.EsusSgSolicitacaoId, x.DataEvento })
            .HasDatabaseName("ix_esussg_evento_solicitacao_data")
            .IsDescending(false, true);

        builder.HasIndex(x => new { x.Usuario, x.DataEvento })
            .HasDatabaseName("ix_esussg_evento_usuario_data");
    }
}

internal sealed class EsusSgGatilhoConfiguration : IEntityTypeConfiguration<EsusSgGatilho>
{
    public void Configure(EntityTypeBuilder<EsusSgGatilho> builder)
    {
        builder.ToTable("esussg_gatilho");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EsusSgSolicitacaoId).HasColumnName("esussg_solicitacao_id").IsRequired();
        builder.Property(x => x.IdEsusSg).HasColumnName("id_esussg").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.ChaveEvento).HasColumnName("chave_evento").HasMaxLength(80).IsRequired();
        builder.Property(x => x.SituacaoAnterior).HasColumnName("situacao_anterior");
        builder.Property(x => x.SituacaoAtual).HasColumnName("situacao_atual");
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.ProcessadoEm).HasColumnName("processado_em");
        builder.Property(x => x.ProcessadoPor).HasColumnName("processado_por").HasMaxLength(120);

        builder.HasIndex(x => new { x.EsusSgSolicitacaoId, x.Tipo, x.ChaveEvento })
            .IsUnique()
            .HasDatabaseName("ux_esussg_gatilho_solicitacao_tipo_chave");

        builder.HasIndex(x => new { x.Tipo, x.CriadoEm })
            .HasDatabaseName("ix_esussg_gatilho_pendente")
            .HasFilter("processado_em IS NULL");

        builder.HasOne(x => x.EsusSgSolicitacao)
            .WithMany()
            .HasForeignKey(x => x.EsusSgSolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
