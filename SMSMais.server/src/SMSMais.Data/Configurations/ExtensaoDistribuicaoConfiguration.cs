using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class ExtensaoPacoteConfiguration : IEntityTypeConfiguration<ExtensaoPacote>
{
    public void Configure(EntityTypeBuilder<ExtensaoPacote> builder)
    {
        builder.ToTable("extensao_pacote");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Artefato).HasColumnName("artefato").HasConversion<int>().IsRequired();
        builder.Property(p => p.Versao).HasColumnName("versao").HasMaxLength(32).IsRequired();
        builder.Property(p => p.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
        builder.Property(p => p.Tamanho).HasColumnName("tamanho").IsRequired();
        builder.Property(p => p.Conteudo).HasColumnName("conteudo").HasColumnType("bytea").IsRequired();
        builder.Property(p => p.Notas).HasColumnName("notas").HasMaxLength(1000);
        builder.Property(p => p.PublicadoEm).HasColumnName("publicado_em").IsRequired();
        builder.Property(p => p.PublicadoPor).HasColumnName("publicado_por");
        builder.Property(p => p.PublicadoPelaApi).HasColumnName("publicado_pela_api").IsRequired();
        builder.Property(p => p.PromovidoEm).HasColumnName("promovido_em");
        builder.Property(p => p.PromovidoPor).HasColumnName("promovido_por");
        builder.Property(p => p.PromovidoPelaApi).HasColumnName("promovido_pela_api").IsRequired();
        builder.Property(p => p.RetiradoEm).HasColumnName("retirado_em");
        builder.Property(p => p.RetiradoPor).HasColumnName("retirado_por");

        builder.HasIndex(p => new { p.Artefato, p.Versao }).IsUnique().HasDatabaseName("ux_extensao_pacote_artefato_versao");
    }
}

internal sealed class ExtensaoChavePublicacaoConfiguration : IEntityTypeConfiguration<ExtensaoChavePublicacao>
{
    public void Configure(EntityTypeBuilder<ExtensaoChavePublicacao> builder)
    {
        builder.ToTable("extensao_chave_publicacao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ChaveHash).HasColumnName("chave_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Prefixo).HasColumnName("prefixo").HasMaxLength(20).IsRequired();
        builder.Property(c => c.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(c => c.CriadaPor).HasColumnName("criada_por");
        builder.Property(c => c.UltimoUsoEm).HasColumnName("ultimo_uso_em");
        builder.Property(c => c.RevogadaEm).HasColumnName("revogada_em");
        builder.Property(c => c.RevogadaPor).HasColumnName("revogada_por");

        builder.HasIndex(c => c.ChaveHash).IsUnique().HasDatabaseName("ux_extensao_chave_publicacao_hash");
    }
}

internal sealed class ExtensaoDispositivoConfiguration : IEntityTypeConfiguration<ExtensaoDispositivo>
{
    public void Configure(EntityTypeBuilder<ExtensaoDispositivo> builder)
    {
        builder.ToTable("extensao_dispositivo");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.Computador).HasColumnName("computador").HasMaxLength(100).IsRequired();
        builder.Property(d => d.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(d => d.Canal).HasColumnName("canal").HasConversion<int>().IsRequired();
        builder.Property(d => d.AutorizadoEm).HasColumnName("autorizado_em").IsRequired();
        builder.Property(d => d.AutorizadoPor).HasColumnName("autorizado_por");
        builder.Property(d => d.UnidadeId).HasColumnName("unidade_id");
        builder.Property(d => d.UltimoContatoEm).HasColumnName("ultimo_contato_em");
        builder.Property(d => d.VersaoExtensao).HasColumnName("versao_extensao").HasMaxLength(32);
        builder.Property(d => d.VersaoAtualizador).HasColumnName("versao_atualizador").HasMaxLength(32);
        builder.Property(d => d.SituacaoChrome).HasColumnName("situacao_chrome").HasMaxLength(30);
        builder.Property(d => d.RevogadoEm).HasColumnName("revogado_em");
        builder.Property(d => d.RevogadoPor).HasColumnName("revogado_por");

        builder.HasIndex(d => d.TokenHash).IsUnique().HasDatabaseName("ux_extensao_dispositivo_token_hash");
    }
}

internal sealed class ExtensaoAtivacaoConfiguration : IEntityTypeConfiguration<ExtensaoAtivacao>
{
    public void Configure(EntityTypeBuilder<ExtensaoAtivacao> builder)
    {
        builder.ToTable("extensao_ativacao");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(64).IsRequired();
        builder.Property(a => a.CodigoPublico).HasColumnName("codigo_publico").HasMaxLength(16).IsRequired();
        builder.Property(a => a.Computador).HasColumnName("computador").HasMaxLength(100);
        builder.Property(a => a.VersaoAtualizador).HasColumnName("versao_atualizador").HasMaxLength(32);
        builder.Property(a => a.PeloInstalador).HasColumnName("pelo_instalador").IsRequired();
        builder.Property(a => a.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(a => a.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(a => a.AutorizadoEm).HasColumnName("autorizado_em");
        builder.Property(a => a.AutorizadoPor).HasColumnName("autorizado_por");
        builder.Property(a => a.UnidadeId).HasColumnName("unidade_id");
        builder.Property(a => a.UsadoEm).HasColumnName("usado_em");
        builder.Property(a => a.DispositivoId).HasColumnName("dispositivo_id");

        builder.HasIndex(a => a.CodigoHash).IsUnique().HasDatabaseName("ux_extensao_ativacao_codigo_hash");
        builder.HasIndex(a => a.CodigoPublico).IsUnique().HasDatabaseName("ux_extensao_ativacao_codigo_publico");
        builder.HasIndex(a => a.ExpiraEm).HasDatabaseName("ix_extensao_ativacao_expira_em");
    }
}
