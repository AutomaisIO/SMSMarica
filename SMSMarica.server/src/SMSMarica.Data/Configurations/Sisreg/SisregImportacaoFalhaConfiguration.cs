using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Data.Configurations.Sisreg;

internal sealed class SisregImportacaoFalhaConfiguration : IEntityTypeConfiguration<SisregImportacaoFalha>
{
    public void Configure(EntityTypeBuilder<SisregImportacaoFalha> builder)
    {
        builder.ToTable("sisreg_importacao_falha");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(40);
        builder.Property(x => x.HashLinha).HasColumnName("hash_linha").HasMaxLength(64).IsRequired();
        // Sem maxLength: é o RAW as-is da linha do SISREG (38 campos) — text.
        builder.Property(x => x.LinhaRaw).HasColumnName("linha_raw").IsRequired();
        builder.Property(x => x.Origem).HasColumnName("origem").HasConversion<int>().IsRequired();
        builder.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(300);
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id");
        builder.Property(x => x.CnesExecutante).HasColumnName("cnes_executante").HasMaxLength(7);
        builder.Property(x => x.NomeExecutante).HasColumnName("nome_executante").HasMaxLength(300);
        builder.Property(x => x.NomePaciente).HasColumnName("nome_paciente").HasMaxLength(300);
        builder.Property(x => x.ProcedimentoTexto).HasColumnName("procedimento_texto").HasMaxLength(500);
        builder.Property(x => x.DataAgendada).HasColumnName("data_agendada");
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.UnidadeExecutanteId).HasColumnName("unidade_executante_id");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
        builder.Property(x => x.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(x => x.ResolvidoPor).HasColumnName("resolvido_por");
        builder.Property(x => x.ResolucaoNota).HasColumnName("resolucao_nota").HasMaxLength(500);
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id");

        // Uma falha PENDENTE por nº do SISREG: reenviar o mesmo arquivo com a linha ainda quebrada
        // atualiza a falha existente em vez de empilhar duplicatas. Falha já resolvida não trava
        // uma reincidência futura (o filtro deixa o nº livre de novo).
        builder.HasIndex(x => x.CodigoSolicitacao)
            .HasDatabaseName("ux_sisreg_falha_codigo_pendente")
            .IsUnique()
            .HasFilter("codigo_solicitacao IS NOT NULL AND resolvido_em IS NULL");

        // Linha sem código legível (parser): o dedupe cai no hash do RAW.
        builder.HasIndex(x => x.HashLinha)
            .HasDatabaseName("ux_sisreg_falha_hash_pendente")
            .IsUnique()
            .HasFilter("codigo_solicitacao IS NULL AND resolvido_em IS NULL");

        // Listagem padrão: pendentes da unidade, mais recentes primeiro.
        builder.HasIndex(x => new { x.ResolvidoEm, x.UnidadeExecutanteId })
            .HasDatabaseName("ix_sisreg_falha_resolvido_unidade");

        // "Ver os erros desta importação" (aba de rastreio).
        builder.HasIndex(x => x.ExecucaoId).HasDatabaseName("ix_sisreg_falha_execucao");
    }
}
