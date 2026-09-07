using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoEventoConfiguration : IEntityTypeConfiguration<RegulacaoEvento>
{
    public void Configure(EntityTypeBuilder<RegulacaoEvento> builder)
    {
        builder.ToTable("regulacao_evento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.StatusAnterior).HasColumnName("status_anterior");
        builder.Property(x => x.StatusNovo).HasColumnName("status_novo");

        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.UsuarioNome).HasColumnName("usuario_nome").HasMaxLength(200);
        builder.Property(x => x.Papel).HasColumnName("papel").IsRequired();
        builder.Property(x => x.UnidadeAtivaId).HasColumnName("unidade_ativa_id");
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(x => x.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);

        builder.Property(x => x.DiffJson).HasColumnName("diff_json").HasColumnType("jsonb");
        builder.Property(x => x.DetalheJson).HasColumnName("detalhe_json").HasColumnType("jsonb");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Cascade: o evento não existe sem a solicitação. É o único lugar do módulo onde cascata
        // faz sentido — em compensação, solicitação não se apaga (soft delete), então na prática
        // nada aqui some.
        builder.HasOne(x => x.Solicitacao).WithMany(s => s.Eventos)
            .HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);

        // A leitura real é sempre "a história desta solicitação, em ordem".
        builder.HasIndex(x => new { x.SolicitacaoId, x.CriadoEm })
            .HasDatabaseName("ix_regulacao_evento_solicitacao");
    }
}

internal sealed class RegulacaoSolicitacaoDestinoConfiguration
    : IEntityTypeConfiguration<RegulacaoSolicitacaoDestino>
{
    public void Configure(EntityTypeBuilder<RegulacaoSolicitacaoDestino> builder)
    {
        builder.ToTable("regulacao_solicitacao_destino");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();
        builder.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(2000);
        builder.Property(x => x.AvaliadoEm).HasColumnName("avaliado_em").IsRequired();

        builder.HasOne(x => x.Solicitacao).WithMany(s => s.Destinos)
            .HasForeignKey(x => x.SolicitacaoId).OnDelete(DeleteBehavior.Cascade);

        // Um veredito por sistema. Sem o unique, reavaliar as regras acumularia vereditos e a tela
        // mostraria "elegível" e "bloqueado" para o mesmo destino.
        builder.HasIndex(x => new { x.SolicitacaoId, x.Sistema })
            .IsUnique()
            .HasDatabaseName("ux_regulacao_destino");
    }
}

internal sealed class RegulacaoEventoVistoConfiguration : IEntityTypeConfiguration<RegulacaoEventoVisto>
{
    public void Configure(EntityTypeBuilder<RegulacaoEventoVisto> builder)
    {
        builder.ToTable("regulacao_evento_visto");

        // Chave composta: um "visto" por evento e por pessoa. Sem isso, marcar de novo criaria
        // linha nova e a contagem de não vistas ficaria negativa na prática.
        builder.HasKey(x => new { x.EventoId, x.UsuarioId });

        builder.Property(x => x.EventoId).HasColumnName("evento_id");
        builder.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        builder.Property(x => x.VistoEm).HasColumnName("visto_em").IsRequired();

        builder.HasOne(x => x.Evento).WithMany()
            .HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Cascade);

        // A pergunta real é sempre "o que ESTE usuário ainda não viu".
        builder.HasIndex(x => x.UsuarioId).HasDatabaseName("ix_regulacao_evento_visto_usuario");
    }
}
