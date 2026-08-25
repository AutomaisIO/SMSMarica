using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitSolicitacaoConfiguration : IEntityTypeConfiguration<SernitSolicitacao>
{
    public void Configure(EntityTypeBuilder<SernitSolicitacao> builder)
    {
        builder.ToTable("sernit_solicitacao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IdSernit).HasColumnName("id_sernit").HasMaxLength(20).IsRequired();

        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Recurso).HasColumnName("recurso").HasMaxLength(300).IsRequired();
        builder.Property(x => x.DataSolicitacao).HasColumnName("data_solicitacao");
        builder.Property(x => x.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IdadeTexto).HasColumnName("idade_texto").HasMaxLength(80);
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(x => x.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(x => x.Cid).HasColumnName("cid").HasMaxLength(300);
        builder.Property(x => x.SolicitanteNome).HasColumnName("solicitante_nome").HasMaxLength(200);
        builder.Property(x => x.MunicipioSolicitante).HasColumnName("municipio_solicitante").HasMaxLength(120);
        builder.Property(x => x.UnidadeExecutora).HasColumnName("unidade_executora").HasMaxLength(300);
        builder.Property(x => x.AgendadoParaTexto).HasColumnName("agendado_para_texto").HasMaxLength(300);
        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();

        builder.Property(x => x.NomeMae).HasColumnName("nome_mae").HasMaxLength(200);
        builder.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(10);
        builder.Property(x => x.DataNascimento).HasColumnName("data_nascimento");
        builder.Property(x => x.Etnia).HasColumnName("etnia").HasMaxLength(80);
        builder.Property(x => x.Cep).HasColumnName("cep").HasMaxLength(10);
        builder.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
        builder.Property(x => x.MunicipioPaciente).HasColumnName("municipio_paciente").HasMaxLength(120);
        builder.Property(x => x.Bairro).HasColumnName("bairro").HasMaxLength(150);
        builder.Property(x => x.TipoLogradouro).HasColumnName("tipo_logradouro").HasMaxLength(40);
        builder.Property(x => x.Logradouro).HasColumnName("logradouro").HasMaxLength(200);
        builder.Property(x => x.Numero).HasColumnName("numero").HasMaxLength(30);
        builder.Property(x => x.Complemento).HasColumnName("complemento").HasMaxLength(150);
        builder.Property(x => x.TelefoneResidencial).HasColumnName("telefone_residencial").HasMaxLength(30);
        builder.Property(x => x.TelefoneWhatsapp).HasColumnName("telefone_whatsapp").HasMaxLength(30);
        builder.Property(x => x.TelefoneContato).HasColumnName("telefone_contato").HasMaxLength(30);

        builder.Property(x => x.PacienteId).HasColumnName("paciente_id");
        builder.Property(x => x.PacienteConciliarEm).HasColumnName("paciente_conciliar_em");
        builder.HasIndex(x => x.PacienteConciliarEm)
            .HasFilter("paciente_conciliar_em IS NOT NULL")
            .HasDatabaseName("ix_sernit_solicitacao_paciente_conciliar");

        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();
        builder.Property(x => x.HistoricoLidoEm).HasColumnName("historico_lido_em");
        builder.Property(x => x.EventosCount).HasColumnName("eventos_count").IsRequired();
        builder.Property(x => x.UltimoEventoEm).HasColumnName("ultimo_evento_em");
        builder.Property(x => x.HistoricoIndisponivel).HasColumnName("historico_indisponivel").IsRequired();
        builder.Property(x => x.SituacaoMudouEm).HasColumnName("situacao_mudou_em");
        builder.Property(x => x.SituacaoAnterior).HasColumnName("situacao_anterior");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        // Chave natural do SERNIT — torna a re-varredura idempotente. Filtrada pelo soft-delete.
        builder.HasIndex(x => x.IdSernit)
            .IsUnique()
            .HasDatabaseName("ux_sernit_solicitacao_id_sernit")
            .HasFilter("excluido_em IS NULL");

        builder.HasIndex(x => new { x.Situacao, x.DataSolicitacao })
            .HasDatabaseName("ix_sernit_solicitacao_situacao_data");

        builder.HasIndex(x => x.Cpf).HasDatabaseName("ix_sernit_solicitacao_cpf");
        builder.HasIndex(x => x.Cns).HasDatabaseName("ix_sernit_solicitacao_cns");

        builder.HasIndex(x => x.PacienteId)
            .HasFilter("paciente_id IS NOT NULL AND excluido_em IS NULL")
            .HasDatabaseName("ix_sernit_solicitacao_paciente_id");

        builder.HasIndex(x => new { x.Situacao, x.HistoricoLidoEm })
            .HasDatabaseName("ix_sernit_solicitacao_situacao_historico_lido");

        builder.HasMany(x => x.Eventos)
            .WithOne(e => e.SernitSolicitacao!)
            .HasForeignKey(e => e.SernitSolicitacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
