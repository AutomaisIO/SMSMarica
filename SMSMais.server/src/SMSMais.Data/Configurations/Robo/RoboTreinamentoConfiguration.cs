using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboTreinamentoItemConfiguration : IEntityTypeConfiguration<RoboTreinamentoItem>
{
    public void Configure(EntityTypeBuilder<RoboTreinamentoItem> builder)
    {
        builder.ToTable("robo_treinamento_item");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RoboErroRespostaId).HasColumnName("robo_erro_resposta_id");
        builder.Property(e => e.ConversaId).HasColumnName("conversa_id");
        builder.Property(e => e.MensagemWhatsAppId).HasColumnName("mensagem_whatsapp_id");
        builder.Property(e => e.RoboAssuntoId).HasColumnName("robo_assunto_id");
        builder.Property(e => e.Trecho).HasColumnName("trecho").HasColumnType("text");
        builder.Property(e => e.ContextoJson).HasColumnName("contexto_json").HasColumnType("jsonb");
        builder.Property(e => e.Critica).HasColumnName("critica").HasColumnType("text").IsRequired();
        builder.Property(e => e.Observacao).HasColumnName("observacao").HasColumnType("text");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.Analise).HasColumnName("analise").HasColumnType("text");
        builder.Property(e => e.AnaliseJson).HasColumnName("analise_json").HasColumnType("jsonb");
        builder.Property(e => e.Modelo).HasColumnName("modelo").HasMaxLength(100);
        builder.Property(e => e.TokensEntrada).HasColumnName("tokens_entrada");
        builder.Property(e => e.TokensSaida).HasColumnName("tokens_saida");
        builder.Property(e => e.CustoUsd).HasColumnName("custo_usd").HasPrecision(12, 6);
        builder.Property(e => e.AnalisadoEm).HasColumnName("analisado_em");
        builder.Property(e => e.TentativasAnalise).HasColumnName("tentativas_analise");
        builder.Property(e => e.ErroMensagem).HasColumnName("erro_mensagem").HasColumnType("text");

        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(e => e.AtualizadoPor).HasColumnName("atualizado_por");

        builder.Property(e => e.RowVersion).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.HasOne(e => e.Conversa).WithMany().HasForeignKey(e => e.ConversaId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.RoboAssunto).WithMany().HasForeignKey(e => e.RoboAssuntoId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Mensagem).WithMany().HasForeignKey(e => e.MensagemWhatsAppId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.RoboErroResposta).WithMany().HasForeignKey(e => e.RoboErroRespostaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Um item por captura: clicar "treinar" duas vezes na mesma bolha não abre dois processos.
        builder.HasIndex(e => e.RoboErroRespostaId)
            .HasDatabaseName("ux_robo_treinamento_erro")
            .IsUnique()
            .HasFilter("robo_erro_resposta_id IS NOT NULL");

        builder.HasIndex(e => new { e.Status, e.CriadoEm })
            .HasDatabaseName("ix_robo_treinamento_status_criado");
    }
}

internal sealed class RoboTreinamentoPendenciaConfiguration : IEntityTypeConfiguration<RoboTreinamentoPendencia>
{
    public void Configure(EntityTypeBuilder<RoboTreinamentoPendencia> builder)
    {
        builder.ToTable("robo_treinamento_pendencia");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RoboTreinamentoItemId).HasColumnName("robo_treinamento_item_id").IsRequired();
        builder.Property(e => e.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(e => e.Pergunta).HasColumnName("pergunta").HasColumnType("text").IsRequired();
        builder.Property(e => e.Contexto).HasColumnName("contexto").HasColumnType("text");
        builder.Property(e => e.OpcoesJson).HasColumnName("opcoes_json").HasColumnType("jsonb");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.Resposta).HasColumnName("resposta").HasColumnType("text");
        builder.Property(e => e.Autorizado).HasColumnName("autorizado");
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.RespondidoEm).HasColumnName("respondido_em");
        builder.Property(e => e.RespondidoPor).HasColumnName("respondido_por");

        builder.HasOne(e => e.Item).WithMany(i => i.Pendencias)
            .HasForeignKey(e => e.RoboTreinamentoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.RoboTreinamentoItemId, e.Status })
            .HasDatabaseName("ix_robo_treinamento_pendencia_item");
    }
}

internal sealed class RoboTreinamentoAlteracaoConfiguration : IEntityTypeConfiguration<RoboTreinamentoAlteracao>
{
    public void Configure(EntityTypeBuilder<RoboTreinamentoAlteracao> builder)
    {
        builder.ToTable("robo_treinamento_alteracao");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RoboTreinamentoItemId).HasColumnName("robo_treinamento_item_id").IsRequired();
        builder.Property(e => e.Alvo).HasColumnName("alvo").HasConversion<int>().IsRequired();
        builder.Property(e => e.Operacao).HasColumnName("operacao").HasConversion<int>().IsRequired();
        builder.Property(e => e.RoboAssuntoId).HasColumnName("robo_assunto_id").IsRequired();
        builder.Property(e => e.AlvoId).HasColumnName("alvo_id").IsRequired();
        builder.Property(e => e.ValorAnteriorJson).HasColumnName("valor_anterior_json").HasColumnType("jsonb");
        builder.Property(e => e.ValorNovoJson).HasColumnName("valor_novo_json").HasColumnType("jsonb");
        builder.Property(e => e.Justificativa).HasColumnName("justificativa").HasColumnType("text");
        builder.Property(e => e.AplicadoEm).HasColumnName("aplicado_em").IsRequired();
        builder.Property(e => e.DesfeitoEm).HasColumnName("desfeito_em");
        builder.Property(e => e.DesfeitoPor).HasColumnName("desfeito_por");

        builder.HasOne(e => e.Item).WithMany(i => i.Alteracoes)
            .HasForeignKey(e => e.RoboTreinamentoItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.RoboAssunto).WithMany().HasForeignKey(e => e.RoboAssuntoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.RoboTreinamentoItemId)
            .HasDatabaseName("ix_robo_treinamento_alteracao_item");
    }
}

internal sealed class RoboTreinamentoSimulacaoConfiguration : IEntityTypeConfiguration<RoboTreinamentoSimulacao>
{
    public void Configure(EntityTypeBuilder<RoboTreinamentoSimulacao> builder)
    {
        builder.ToTable("robo_treinamento_simulacao");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.RoboTreinamentoItemId).HasColumnName("robo_treinamento_item_id").IsRequired();
        builder.Property(e => e.Mensagem).HasColumnName("mensagem").HasColumnType("text").IsRequired();
        builder.Property(e => e.HistoricoJson).HasColumnName("historico_json").HasColumnType("jsonb");
        builder.Property(e => e.AssuntoNome).HasColumnName("assunto_nome").HasMaxLength(200);
        builder.Property(e => e.Resposta).HasColumnName("resposta").HasColumnType("text");
        builder.Property(e => e.ChamadasJson).HasColumnName("chamadas_json").HasColumnType("jsonb");
        builder.Property(e => e.Veredito).HasColumnName("veredito").HasConversion<int?>();
        builder.Property(e => e.Analise).HasColumnName("analise").HasColumnType("text");
        builder.Property(e => e.TokensEntrada).HasColumnName("tokens_entrada");
        builder.Property(e => e.TokensSaida).HasColumnName("tokens_saida");
        builder.Property(e => e.CustoUsd).HasColumnName("custo_usd").HasPrecision(12, 6);
        builder.Property(e => e.DuracaoMs).HasColumnName("duracao_ms");
        builder.Property(e => e.Automatica).HasColumnName("automatica").IsRequired();
        builder.Property(e => e.ErroMensagem).HasColumnName("erro_mensagem").HasColumnType("text");
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(e => e.Item).WithMany(i => i.Simulacoes)
            .HasForeignKey(e => e.RoboTreinamentoItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.RoboTreinamentoItemId, e.CriadoEm })
            .HasDatabaseName("ix_robo_treinamento_simulacao_item");
    }
}
