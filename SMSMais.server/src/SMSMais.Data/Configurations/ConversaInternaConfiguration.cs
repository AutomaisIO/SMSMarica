using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.ComunicacaoInterna;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Configurations;

internal sealed class ConversaInternaConfiguration : IEntityTypeConfiguration<ConversaInterna>
{
    public void Configure(EntityTypeBuilder<ConversaInterna> builder)
    {
        builder.ToTable("conversa_interna", t =>
        {
            // Cada tipo usa exatamente o seu par, já ordenado (A ≤ B) — é o que torna os índices
            // únicos abaixo a garantia de "uma sessão por par".
            t.HasCheckConstraint("ck_conversa_interna_par",
                "(tipo = 1 AND unidade_a_id IS NOT NULL AND unidade_b_id IS NOT NULL AND unidade_a_id <= unidade_b_id"
                + " AND usuario_a_id IS NULL AND usuario_b_id IS NULL)"
                + " OR (tipo = 2 AND usuario_a_id IS NOT NULL AND usuario_b_id IS NOT NULL AND usuario_a_id < usuario_b_id"
                + " AND unidade_a_id IS NULL AND unidade_b_id IS NULL)");
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(c => c.UnidadeAId).HasColumnName("unidade_a_id");
        builder.Property(c => c.UnidadeBId).HasColumnName("unidade_b_id");
        builder.Property(c => c.UsuarioAId).HasColumnName("usuario_a_id");
        builder.Property(c => c.UsuarioBId).HasColumnName("usuario_b_id");
        builder.Property(c => c.UltimaMensagemEm).HasColumnName("ultima_mensagem_em");
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(c => new { c.UnidadeAId, c.UnidadeBId })
            .IsUnique()
            .HasFilter($"tipo = {(int)TipoConversaInterna.Unidade}")
            .HasDatabaseName("ux_conversa_interna_unidades");
        builder.HasIndex(c => new { c.UsuarioAId, c.UsuarioBId })
            .IsUnique()
            .HasFilter($"tipo = {(int)TipoConversaInterna.Direta}")
            .HasDatabaseName("ux_conversa_interna_usuarios");
        // A lista "minhas conversas" procura pelo lado B também.
        builder.HasIndex(c => c.UnidadeBId);
        builder.HasIndex(c => c.UsuarioBId);

        builder.HasOne(c => c.UnidadeA).WithMany().HasForeignKey(c => c.UnidadeAId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.UnidadeB).WithMany().HasForeignKey(c => c.UnidadeBId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.UsuarioA).WithMany().HasForeignKey(c => c.UsuarioAId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.UsuarioB).WithMany().HasForeignKey(c => c.UsuarioBId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Mensagens)
            .WithOne(m => m.Conversa)
            .HasForeignKey(m => m.ConversaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MensagemInternaConfiguration : IEntityTypeConfiguration<MensagemInterna>
{
    public void Configure(EntityTypeBuilder<MensagemInterna> builder)
    {
        builder.ToTable("mensagem_interna", t =>
            t.HasCheckConstraint("ck_mensagem_interna_conteudo", "texto IS NOT NULL OR anexo_chave IS NOT NULL"));
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ConversaId).HasColumnName("conversa_id").IsRequired();
        builder.Property(m => m.AutorId).HasColumnName("autor_id").IsRequired();
        builder.Property(m => m.AutorUnidadeId).HasColumnName("autor_unidade_id");
        builder.Property(m => m.Texto).HasColumnName("texto").HasMaxLength(8000);
        builder.Property(m => m.AnexoNome).HasColumnName("anexo_nome").HasMaxLength(255);
        builder.Property(m => m.AnexoMime).HasColumnName("anexo_mime").HasMaxLength(150);
        builder.Property(m => m.AnexoTamanhoBytes).HasColumnName("anexo_tamanho_bytes");
        builder.Property(m => m.AnexoChave).HasColumnName("anexo_chave").HasMaxLength(500);
        builder.Property(m => m.AnexoEhAudio).HasColumnName("anexo_eh_audio").IsRequired();
        builder.Property(m => m.AnexoDuracaoSegundos).HasColumnName("anexo_duracao_segundos");
        builder.Property(m => m.EnviadaEm).HasColumnName("enviada_em").IsRequired();
        builder.Property(m => m.Transcricao).HasColumnName("transcricao");
        builder.Property(m => m.TranscritaEm).HasColumnName("transcrita_em");
        builder.Property(m => m.TranscritaPor).HasColumnName("transcrita_por");

        // Página da thread (mais recentes primeiro, cursor por enviada_em) e contagem de não-lidas.
        builder.HasIndex(m => new { m.ConversaId, m.EnviadaEm }).HasDatabaseName("ix_mensagem_interna_conversa_enviada");
        builder.HasIndex(m => m.AutorId);
        // O índice trigram da busca (f_unaccent(texto)) é criado em SQL na migration.

        builder.HasOne(m => m.Autor).WithMany().HasForeignKey(m => m.AutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AutorUnidade).WithMany().HasForeignKey(m => m.AutorUnidadeId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ConversaInternaLeituraConfiguration : IEntityTypeConfiguration<ConversaInternaLeitura>
{
    public void Configure(EntityTypeBuilder<ConversaInternaLeitura> builder)
    {
        builder.ToTable("conversa_interna_leitura");
        builder.HasKey(l => new { l.ConversaId, l.UsuarioId });

        builder.Property(l => l.ConversaId).HasColumnName("conversa_id");
        builder.Property(l => l.UsuarioId).HasColumnName("usuario_id");
        builder.Property(l => l.LidaAte).HasColumnName("lida_ate");
        builder.Property(l => l.Silenciada).HasColumnName("silenciada").IsRequired();

        builder.HasIndex(l => l.UsuarioId);

        builder.HasOne(l => l.Conversa).WithMany().HasForeignKey(l => l.ConversaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Entities.Usuario>().WithMany().HasForeignKey(l => l.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
