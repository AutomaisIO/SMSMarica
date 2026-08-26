using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Data.Configurations.Robo;

internal sealed class RoboAssuntoConfiguration : IEntityTypeConfiguration<RoboAssunto>
{
    public void Configure(EntityTypeBuilder<RoboAssunto> builder)
    {
        builder.ToTable("robo_assunto");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(a => a.Descricao).HasColumnName("descricao").HasMaxLength(500);
        builder.Property(a => a.InstrucoesPersona).HasColumnName("instrucoes_persona").HasColumnType("text").IsRequired();
        builder.Property(a => a.Modelo).HasColumnName("modelo").HasMaxLength(100);
        builder.Property(a => a.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(a => a.HorarioInicio).HasColumnName("horario_inicio");
        builder.Property(a => a.HorarioFim).HasColumnName("horario_fim");
        builder.Property(a => a.DiasSemana).HasColumnName("dias_semana");
        builder.Property(a => a.MaxInteracoesSemResolver).HasColumnName("max_interacoes_sem_resolver").HasDefaultValue(5).IsRequired();
        builder.Property(a => a.LimiarConfianca).HasColumnName("limiar_confianca").HasDefaultValue(0.6).IsRequired();
        builder.Property(a => a.EscalonamentoUnidadeId).HasColumnName("escalonamento_unidade_id");
        builder.Property(a => a.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();

        builder.Property(a => a.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.CriadoPor).HasColumnName("criado_por");
        builder.Property(a => a.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(a => a.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(a => a.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(a => a.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(a => a.EscalonamentoUnidade)
            .WithMany()
            .HasForeignKey(a => a.EscalonamentoUnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Condicoes)
            .WithOne(c => c.RoboAssunto!)
            .HasForeignKey(c => c.RoboAssuntoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Treinos)
            .WithOne(t => t.RoboAssunto!)
            .HasForeignKey(t => t.RoboAssuntoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Comandos)
            .WithOne(c => c.RoboAssunto!)
            .HasForeignKey(c => c.RoboAssuntoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.Nome)
            .HasDatabaseName("ux_robo_assunto_nome")
            .IsUnique()
            .HasFilter("excluido_em IS NULL");

        builder.HasIndex(a => a.Ordem)
            .HasDatabaseName("ix_robo_assunto_ordem")
            .HasFilter("excluido_em IS NULL");
    }
}

internal sealed class RoboAssuntoCondicaoConfiguration : IEntityTypeConfiguration<RoboAssuntoCondicao>
{
    public void Configure(EntityTypeBuilder<RoboAssuntoCondicao> builder)
    {
        builder.ToTable("robo_assunto_condicao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RoboAssuntoId).HasColumnName("robo_assunto_id").IsRequired();
        builder.Property(c => c.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(c => c.Valor).HasColumnName("valor").HasMaxLength(400).IsRequired();
        builder.Property(c => c.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(c => c.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();

        builder.HasIndex(c => new { c.RoboAssuntoId, c.Ordem });
    }
}

internal sealed class RoboAssuntoTreinoConfiguration : IEntityTypeConfiguration<RoboAssuntoTreino>
{
    public void Configure(EntityTypeBuilder<RoboAssuntoTreino> builder)
    {
        builder.ToTable("robo_assunto_treino");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.RoboAssuntoId).HasColumnName("robo_assunto_id").IsRequired();
        builder.Property(t => t.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(t => t.Titulo).HasColumnName("titulo").HasMaxLength(200);
        builder.Property(t => t.Conteudo).HasColumnName("conteudo").HasColumnType("text").IsRequired();
        builder.Property(t => t.Ordem).HasColumnName("ordem").HasDefaultValue(0).IsRequired();
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();

        builder.HasIndex(t => new { t.RoboAssuntoId, t.Ordem });
    }
}

internal sealed class RoboAssuntoComandoConfiguration : IEntityTypeConfiguration<RoboAssuntoComando>
{
    public void Configure(EntityTypeBuilder<RoboAssuntoComando> builder)
    {
        builder.ToTable("robo_assunto_comando");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RoboAssuntoId).HasColumnName("robo_assunto_id").IsRequired();
        builder.Property(c => c.Comando).HasColumnName("comando").HasConversion<int>().IsRequired();
        builder.Property(c => c.Habilitado).HasColumnName("habilitado").IsRequired();

        // Um assunto tem no máximo uma linha por comando (liga/desliga).
        builder.HasIndex(c => new { c.RoboAssuntoId, c.Comando }).IsUnique();
    }
}
