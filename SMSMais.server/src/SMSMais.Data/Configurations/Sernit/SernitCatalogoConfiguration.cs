using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Data.Configurations.Sernit;

internal sealed class SernitCatalogoRecursoConfiguration : IEntityTypeConfiguration<SernitCatalogoRecurso>
{
    public void Configure(EntityTypeBuilder<SernitCatalogoRecurso> builder)
    {
        builder.ToTable("sernit_catalogo_recurso");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Tipo).HasColumnName("tipo").IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();
        builder.Property(x => x.CamposLidos).HasColumnName("campos_lidos").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CidListaId).HasColumnName("cid_lista_id");
        builder.Property(x => x.CidAssinatura).HasColumnName("cid_assinatura").HasMaxLength(60);

        // Chave natural: (tipo, value do combo). O SERNIT não tem o ramo "ambulatório estadual".
        builder.HasIndex(x => new { x.Tipo, x.Valor })
            .IsUnique().HasDatabaseName("ux_sernit_catalogo_recurso");

        builder.HasMany(x => x.Campos)
            .WithOne(x => x.Recurso!)
            .HasForeignKey(x => x.RecursoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CidLista)
            .WithMany()
            .HasForeignKey(x => x.CidListaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SernitCatalogoCidListaConfiguration : IEntityTypeConfiguration<SernitCatalogoCidLista>
{
    public void Configure(EntityTypeBuilder<SernitCatalogoCidLista> builder)
    {
        builder.ToTable("sernit_catalogo_cid_lista");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Assinatura).HasColumnName("assinatura").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Quantidade).HasColumnName("quantidade").IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();

        builder.HasIndex(x => x.Assinatura)
            .IsUnique().HasDatabaseName("ux_sernit_catalogo_cid_lista");

        builder.HasMany(x => x.Cids)
            .WithOne(x => x.Lista!)
            .HasForeignKey(x => x.ListaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SernitCatalogoCidConfiguration : IEntityTypeConfiguration<SernitCatalogoCid>
{
    public void Configure(EntityTypeBuilder<SernitCatalogoCid> builder)
    {
        builder.ToTable("sernit_catalogo_cid");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ListaId).HasColumnName("lista_id").IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(400).IsRequired();
        builder.Property(x => x.Texto).HasColumnName("texto").HasMaxLength(420).IsRequired();
        builder.Property(x => x.Busca).HasColumnName("busca").HasMaxLength(420).IsRequired();

        builder.HasIndex(x => new { x.ListaId, x.Codigo })
            .IsUnique().HasDatabaseName("ux_sernit_catalogo_cid");

        builder.HasIndex(x => new { x.ListaId, x.Busca })
            .HasDatabaseName("ix_sernit_catalogo_cid_busca");
    }
}

internal sealed class SernitCatalogoCampoConfiguration : IEntityTypeConfiguration<SernitCatalogoCampo>
{
    public void Configure(EntityTypeBuilder<SernitCatalogoCampo> builder)
    {
        builder.ToTable("sernit_catalogo_campo");
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
            .IsUnique().HasDatabaseName("ux_sernit_catalogo_campo");
    }
}

internal sealed class SernitCatalogoListaConfiguration : IEntityTypeConfiguration<SernitCatalogoLista>
{
    public void Configure(EntityTypeBuilder<SernitCatalogoLista> builder)
    {
        builder.ToTable("sernit_catalogo_lista");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Lista).HasColumnName("lista").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Valor).HasColumnName("valor").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();
        builder.Property(x => x.SincronizadoEm).HasColumnName("sincronizado_em").IsRequired();

        builder.HasIndex(x => new { x.Lista, x.Valor })
            .IsUnique().HasDatabaseName("ux_sernit_catalogo_lista");
    }
}
