using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Data.Configurations.Ser;

internal sealed class SerCatalogoRecursoConfiguration : IEntityTypeConfiguration<SerCatalogoRecurso>
{
    public void Configure(EntityTypeBuilder<SerCatalogoRecurso> builder)
    {
        builder.ToTable("ser_catalogo_recurso");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();
        builder.Property(x => x.CamposLidos).HasColumnName("campos_lidos").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.AmbulatorioEstadual)
            .HasColumnName("ambulatorio_estadual").IsRequired().HasDefaultValue(false);

        // A chave natural do recurso no SER é (tipo, ramo, value do combo) — o RAMO faz parte da
        // identidade porque o mesmo `value` aparece nos dois com formulários diferentes (ver
        // SerCatalogoRecurso.AmbulatorioEstadual). Deixar o ramo de fora faria as duas versões
        // brigarem pela mesma linha, e a última cópia venceria em silêncio.
        builder.HasIndex(x => new { x.Tipo, x.AmbulatorioEstadual, x.Valor })
            .IsUnique().HasDatabaseName("ux_ser_catalogo_recurso");

        builder.HasMany(x => x.Campos)
            .WithOne(x => x.Recurso!)
            .HasForeignKey(x => x.RecursoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.CidListaId).HasColumnName("cid_lista_id");
        builder.Property(x => x.CidAssinatura).HasColumnName("cid_assinatura").HasMaxLength(60);

        // `Restrict`: a lista de CID é copiada uma vez e compartilhada por centenas de recursos.
        // Apagar uma lista por tabela em cascata deixaria os recursos apontando para o vazio sem
        // ninguém perceber — quem troca a lista de um recurso é a cópia do catálogo.
        builder.HasOne(x => x.CidLista)
            .WithMany()
            .HasForeignKey(x => x.CidListaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SerCatalogoCidListaConfiguration
    : IEntityTypeConfiguration<SerCatalogoCidLista>
{
    public void Configure(EntityTypeBuilder<SerCatalogoCidLista> builder)
    {
        builder.ToTable("ser_catalogo_cid_lista");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Assinatura).HasColumnName("assinatura").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();

        // A assinatura É a identidade da lista: dois recursos que respondem o mesmo às buscas de
        // sondagem compartilham a lista, e é assim que um recurso novo se liga a uma cópia que já
        // existe sem varrer 260 prefixos de novo.
        builder.HasIndex(x => x.Assinatura)
            .IsUnique().HasDatabaseName("ux_ser_catalogo_cid_lista");

        builder.HasMany(x => x.Cids)
            .WithOne(x => x.Lista!)
            .HasForeignKey(x => x.ListaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SerCatalogoCidConfiguration : IEntityTypeConfiguration<SerCatalogoCid>
{
    public void Configure(EntityTypeBuilder<SerCatalogoCid> builder)
    {
        builder.ToTable("ser_catalogo_cid");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListaId).HasColumnName("lista_id").IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(400).IsRequired();
        builder.Property(x => x.Texto).HasColumnName("texto").HasMaxLength(420).IsRequired();
        builder.Property(x => x.Busca).HasColumnName("busca").HasMaxLength(420).IsRequired();

        builder.HasIndex(x => new { x.ListaId, x.Codigo })
            .IsUnique().HasDatabaseName("ux_ser_catalogo_cid");

        // A tela busca "contém" dentro de UMA lista, e são 14 mil linhas por lista. O índice
        // composto é o que mantém o filtro por lista barato antes do LIKE.
        builder.HasIndex(x => new { x.ListaId, x.Busca })
            .HasDatabaseName("ix_ser_catalogo_cid_busca");
    }
}

internal sealed class SerCatalogoCampoConfiguration : IEntityTypeConfiguration<SerCatalogoCampo>
{
    public void Configure(EntityTypeBuilder<SerCatalogoCampo> builder)
    {
        builder.ToTable("ser_catalogo_campo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RecursoId).HasColumnName("recurso_id").IsRequired();
        builder.Property(x => x.Numero).HasColumnName("numero").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Campo).HasColumnName("campo").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Obrigatorio).HasColumnName("obrigatorio").IsRequired();
        builder.Property(x => x.OpcoesJson).HasColumnName("opcoes_json").HasColumnType("jsonb");
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();

        builder.HasIndex(x => new { x.RecursoId, x.Numero })
            .IsUnique()
            .HasDatabaseName("ux_ser_catalogo_campo");
    }
}

internal sealed class SerCatalogoListaConfiguration : IEntityTypeConfiguration<SerCatalogoLista>
{
    public void Configure(EntityTypeBuilder<SerCatalogoLista> builder)
    {
        builder.ToTable("ser_catalogo_lista");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Lista).HasColumnName("lista").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();

        builder.HasIndex(x => new { x.Lista, x.Valor })
            .IsUnique()
            .HasDatabaseName("ux_ser_catalogo_lista");
    }
}
