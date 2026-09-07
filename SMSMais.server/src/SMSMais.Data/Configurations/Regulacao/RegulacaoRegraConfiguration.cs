using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoRegraConfiguration : IEntityTypeConfiguration<RegulacaoRegra>
{
    public void Configure(EntityTypeBuilder<RegulacaoRegra> builder)
    {
        builder.ToTable("regulacao_regra");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProcedimentoId).HasColumnName("procedimento_id").IsRequired();
        builder.Property(x => x.ProcedimentoOrigemId).HasColumnName("procedimento_origem_id");
        builder.Property(x => x.Sistema).HasColumnName("sistema");
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Severidade).HasColumnName("severidade").IsRequired();

        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Fonte).HasColumnName("fonte").HasMaxLength(300);

        builder.Property(x => x.IdadeMinAnos).HasColumnName("idade_min_anos");
        builder.Property(x => x.IdadeMaxAnos).HasColumnName("idade_max_anos");
        builder.Property(x => x.Sexo).HasColumnName("sexo").HasMaxLength(1);
        builder.Property(x => x.ExigeCpf).HasColumnName("exige_cpf").IsRequired().HasDefaultValue(false);

        builder.Property(x => x.CidsPermitidosJson).HasColumnName("cids_permitidos_json").HasColumnType("jsonb");
        builder.Property(x => x.CidsExcluidosJson).HasColumnName("cids_excluidos_json").HasColumnType("jsonb");
        builder.Property(x => x.MunicipiosIbgeJson).HasColumnName("municipios_ibge_json").HasColumnType("jsonb");
        builder.Property(x => x.ExpressaoJson).HasColumnName("expressao_json").HasColumnType("jsonb");

        builder.Property(x => x.Pergunta).HasColumnName("pergunta").HasMaxLength(500);
        builder.Property(x => x.RespostaBloqueia).HasColumnName("resposta_bloqueia");
        builder.Property(x => x.NaoSeiVira).HasColumnName("nao_sei_vira");

        builder.Property(x => x.DocumentoRotulo).HasColumnName("documento_rotulo").HasMaxLength(200);
        builder.Property(x => x.TipoExameId).HasColumnName("tipo_exame_id");
        builder.Property(x => x.ValidadeDias).HasColumnName("validade_dias");
        builder.Property(x => x.Obrigatorio).HasColumnName("obrigatorio").IsRequired().HasDefaultValue(true);

        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Versao).HasColumnName("versao").IsRequired().HasDefaultValue(1);
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired().HasDefaultValue(true);

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(x => x.Procedimento).WithMany()
            .HasForeignKey(x => x.ProcedimentoId).OnDelete(DeleteBehavior.Restrict);

        // `SetNull`: a origem sumindo do catálogo do sistema não pode apagar a regra do manual —
        // ela continua valendo para o canônico.
        builder.HasOne(x => x.ProcedimentoOrigem).WithMany()
            .HasForeignKey(x => x.ProcedimentoOrigemId).OnDelete(DeleteBehavior.SetNull);

        // A pergunta real é sempre "quais regras valem para este procedimento, neste destino".
        builder.HasIndex(x => new { x.ProcedimentoId, x.Sistema, x.Ativo })
            .HasDatabaseName("ix_regulacao_regra_proc_sistema_ativo");

        // Filtro global: regra excluída some de toda consulta sem cada `Where` lembrar disso.
        builder.HasQueryFilter(x => x.ExcluidoEm == null);
    }
}

internal sealed class RegulacaoSolicitacaoRespostaRegraConfiguration
    : IEntityTypeConfiguration<RegulacaoSolicitacaoRespostaRegra>
{
    public void Configure(EntityTypeBuilder<RegulacaoSolicitacaoRespostaRegra> builder)
    {
        builder.ToTable("regulacao_solicitacao_resposta_regra");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.RegraId).HasColumnName("regra_id").IsRequired();
        builder.Property(x => x.RegraVersao).HasColumnName("regra_versao").IsRequired();
        builder.Property(x => x.Resposta).HasColumnName("resposta").IsRequired();
        builder.Property(x => x.ValorDeduzido).HasColumnName("valor_deduzido").HasMaxLength(200);
        builder.Property(x => x.Resultado).HasColumnName("resultado").IsRequired();
        builder.Property(x => x.RespondidoPor).HasColumnName("respondido_por");
        builder.Property(x => x.RespondidoEm).HasColumnName("respondido_em").IsRequired();

        builder.HasOne(x => x.Solicitacao).WithMany()
            .HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);

        // `Restrict`: a regra não se apaga com resposta pendurada — é ela que explica a decisão.
        builder.HasOne(x => x.Regra).WithMany()
            .HasForeignKey(x => x.RegraId).OnDelete(DeleteBehavior.Restrict);

        // Uma resposta por regra. Reavaliar atualiza a linha em vez de empilhar vereditos.
        builder.HasIndex(x => new { x.SolicitacaoId, x.RegraId })
            .IsUnique()
            .HasDatabaseName("ux_regulacao_resposta");
    }
}
