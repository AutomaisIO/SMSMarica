using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoSolicitacaoConfiguration
    : IEntityTypeConfiguration<RegulacaoSolicitacao>
{
    public void Configure(EntityTypeBuilder<RegulacaoSolicitacao> builder)
    {
        builder.ToTable("regulacao_solicitacao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        // Identity ALWAYS: o número é do banco. Deixar o app escrever abriria espaço para
        // duplicata quando duas unidades abrem solicitação no mesmo instante.
        builder.Property(x => x.NumeroLocal)
            .HasColumnName("numero_local").UseIdentityAlwaysColumn().ValueGeneratedOnAdd();

        builder.Property(x => x.Fluxo).HasColumnName("fluxo").IsRequired();
        builder.Property(x => x.UnidadeSolicitanteId).HasColumnName("unidade_solicitante_id").IsRequired();
        builder.Property(x => x.UnidadeEmNomeDeId).HasColumnName("unidade_em_nome_de_id");
        builder.Property(x => x.CriadoPorUsuarioId).HasColumnName("criado_por_usuario_id").IsRequired();

        builder.Property(x => x.PacienteId).HasColumnName("paciente_id").IsRequired();
        builder.Property(x => x.PacienteCpf).HasColumnName("paciente_cpf").HasMaxLength(14);
        builder.Property(x => x.PacienteCns).HasColumnName("paciente_cns").HasMaxLength(20);
        builder.Property(x => x.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200).IsRequired();

        builder.Property(x => x.ProcedimentoId).HasColumnName("procedimento_id").IsRequired();
        builder.Property(x => x.SistemaDestino).HasColumnName("sistema_destino");

        builder.Property(x => x.FormularioJson)
            .HasColumnName("formulario_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FormularioVersaoId).HasColumnName("formulario_versao_id");

        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.StatusMotivo).HasColumnName("status_motivo").HasMaxLength(2000);
        builder.Property(x => x.AgenteResponsavelId).HasColumnName("agente_responsavel_id");

        builder.Property(x => x.NumeroExterno).HasColumnName("numero_externo").HasMaxLength(40);
        builder.Property(x => x.EnviadoEm).HasColumnName("enviado_em");
        builder.Property(x => x.EnviadoPorUsuarioId).HasColumnName("enviado_por_usuario_id");
        builder.Property(x => x.CredencialUsadaId).HasColumnName("credencial_usada_id");
        builder.Property(x => x.OperadorExternoLogin).HasColumnName("operador_externo_login").HasMaxLength(120);
        builder.Property(x => x.EnvioAssistido)
            .HasColumnName("envio_assistido").IsRequired().HasDefaultValue(false);

        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id");
        builder.Property(x => x.SerSolicitacaoId).HasColumnName("ser_solicitacao_id");
        builder.Property(x => x.SernitSolicitacaoId).HasColumnName("sernit_solicitacao_id");
        builder.Property(x => x.SisregEditavelAte).HasColumnName("sisreg_editavel_ate");
        builder.Property(x => x.OrigemLegadoId).HasColumnName("origem_legado_id");
        builder.Property(x => x.Observacoes).HasColumnName("observacoes").HasMaxLength(4000);

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // `Restrict` nas unidades: unidade não se apaga com solicitação viva pendurada, e o
        // histórico de "quem pediu" é o que responde auditoria.
        builder.HasOne(x => x.UnidadeSolicitante).WithMany()
            .HasForeignKey(x => x.UnidadeSolicitanteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UnidadeEmNomeDe).WithMany()
            .HasForeignKey(x => x.UnidadeEmNomeDeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Procedimento).WithMany()
            .HasForeignKey(x => x.ProcedimentoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FormularioVersao).WithMany()
            .HasForeignKey(x => x.FormularioVersaoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.NumeroLocal).IsUnique().HasDatabaseName("ux_regulacao_solicitacao_numero");
        builder.HasIndex(x => new { x.UnidadeSolicitanteId, x.Status })
            .HasDatabaseName("ix_regulacao_solicitacao_unidade_status");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_regulacao_solicitacao_status");
        builder.HasIndex(x => x.PacienteId).HasDatabaseName("ix_regulacao_solicitacao_paciente");
        builder.HasIndex(x => x.ExcluidoEm).HasDatabaseName("ix_regulacao_solicitacao_excluido_em");

        // Um rascunho legado vira UMA solicitação. A idempotência do migrador é conferida em
        // código, mas dois cliques simultâneos passariam pela conferência juntos — a trava real
        // é esta. Parcial porque solicitação nascida no wizard não tem origem legada.
        builder.HasIndex(x => x.OrigemLegadoId)
            .IsUnique()
            .HasFilter("origem_legado_id IS NOT NULL")
            .HasDatabaseName("ux_regulacao_solicitacao_origem_legado");

        // Um número externo pertence a UMA solicitação, por sistema. Índice parcial porque só as
        // enviadas têm número, e sem o filtro os nulos colidiriam entre si. É esta trava que
        // impede o mesmo pedido virar dois no SISREG por duplo clique.
        builder.HasIndex(x => new { x.SistemaDestino, x.NumeroExterno })
            .IsUnique()
            .HasFilter("numero_externo IS NOT NULL")
            .HasDatabaseName("ux_regulacao_solicitacao_numero_externo");
    }
}
