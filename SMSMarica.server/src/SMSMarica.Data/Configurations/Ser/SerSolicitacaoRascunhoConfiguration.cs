using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Data.Configurations.Ser;

internal sealed class SerSolicitacaoRascunhoConfiguration
    : IEntityTypeConfiguration<SerSolicitacaoRascunho>
{
    public void Configure(EntityTypeBuilder<SerSolicitacaoRascunho> builder)
    {
        builder.ToTable("ser_solicitacao_rascunho");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo");
        builder.Property(x => x.AmbulatorioEstadual).HasColumnName("ambulatorio_estadual");
        builder.Property(x => x.RecursoValor).HasColumnName("recurso_valor").HasMaxLength(40);
        builder.Property(x => x.RecursoRotulo).HasColumnName("recurso_rotulo").HasMaxLength(300);
        builder.Property(x => x.Cns).HasColumnName("cns").HasMaxLength(20);
        builder.Property(x => x.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200);
        builder.Property(x => x.Hipotese).HasColumnName("hipotese").HasMaxLength(500);

        // jsonb: o conjunto de campos muda por recurso (21 formulários, 163 campos únicos), então
        // colunas fixas exigiriam migration a cada mexida da SES-RJ numa especialidade.
        builder.Property(x => x.CamposJson).HasColumnName("campos_json").HasColumnType("jsonb").IsRequired();

        builder.Property(x => x.IdSerGerado).HasColumnName("id_ser_gerado").HasMaxLength(20);
        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.CriadoPorNome).HasColumnName("criado_por_nome").HasMaxLength(200);
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.EnviadoEm).HasColumnName("enviado_em");

        builder.HasIndex(x => x.Status).HasDatabaseName("ix_ser_rascunho_status");

        // Um número do SER pertence a UM rascunho: índice parcial porque só os enviados têm
        // número, e sem o filtro os nulos colidiriam entre si.
        builder.HasIndex(x => x.IdSerGerado)
            .IsUnique()
            .HasFilter("id_ser_gerado IS NOT NULL")
            .HasDatabaseName("ux_ser_rascunho_id_gerado");

        builder.HasMany(x => x.Anexos)
            .WithOne(x => x.Rascunho!)
            .HasForeignKey(x => x.RascunhoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SerRascunhoAnexoConfiguration : IEntityTypeConfiguration<SerRascunhoAnexo>
{
    public void Configure(EntityTypeBuilder<SerRascunhoAnexo> builder)
    {
        builder.ToTable("ser_rascunho_anexo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RascunhoId).HasColumnName("rascunho_id").IsRequired();
        builder.Property(x => x.MidiaId).HasColumnName("midia_id").IsRequired();
        builder.Property(x => x.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120);
        builder.Property(x => x.Tamanho).HasColumnName("tamanho").IsRequired();
        builder.Property(x => x.EnviadoEm).HasColumnName("enviado_em");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(x => x.RascunhoId).HasDatabaseName("ix_ser_rascunho_anexo_rascunho");
    }
}
