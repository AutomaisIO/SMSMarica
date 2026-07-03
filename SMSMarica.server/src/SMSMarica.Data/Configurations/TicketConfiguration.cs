using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("ticket");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Descricao).HasColumnName("descricao").IsRequired();

        builder.Property(t => t.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(t => t.Prioridade).HasColumnName("prioridade").HasConversion<int>().IsRequired();

        builder.Property(t => t.RespostaFinal).HasColumnName("resposta_final");
        builder.Property(t => t.UnidadeId).HasColumnName("unidade_id");

        builder.Property(t => t.ArquivadoPeloAutorEm).HasColumnName("arquivado_pelo_autor_em");
        builder.Property(t => t.ArquivadoPeloAdminEm).HasColumnName("arquivado_pelo_admin_em");

        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(t => t.CriadoPor).HasColumnName("criado_por");
        builder.Property(t => t.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(t => t.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(t => t.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(t => t.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(t => t.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(t => t.CriadoPor);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.UnidadeId);

        builder.HasMany(t => t.Comentarios)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Anexos)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketComentarioConfiguration : IEntityTypeConfiguration<TicketComentario>
{
    public void Configure(EntityTypeBuilder<TicketComentario> builder)
    {
        builder.ToTable("ticket_comentario");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(c => c.AutorId).HasColumnName("autor_id");
        builder.Property(c => c.Texto).HasColumnName("texto").IsRequired();
        builder.Property(c => c.Interno).HasColumnName("interno").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(c => c.TicketId);

        builder.HasMany(c => c.Anexos)
            .WithOne(a => a.Comentario)
            .HasForeignKey(a => a.ComentarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TicketAnexoConfiguration : IEntityTypeConfiguration<TicketAnexo>
{
    public void Configure(EntityTypeBuilder<TicketAnexo> builder)
    {
        builder.ToTable("ticket_anexo");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(a => a.ComentarioId).HasColumnName("comentario_id");
        builder.Property(a => a.MidiaId).HasColumnName("midia_id").IsRequired();
        builder.Property(a => a.NomeArquivo).HasColumnName("nome_arquivo").HasMaxLength(300).IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(a => a.TicketId);
    }
}

internal sealed class TicketConfiguracaoConfiguration : IEntityTypeConfiguration<TicketConfiguracao>
{
    public void Configure(EntityTypeBuilder<TicketConfiguracao> builder)
    {
        builder.ToTable("ticket_configuracao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Visibilidade).HasColumnName("visibilidade").HasConversion<int>().IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");

        builder.Property(c => c.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
