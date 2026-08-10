using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Data.Configurations.Ser;

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

        // O SER identifica o recurso pelo par (tipo, value do combo) — é a chave natural, e o
        // índice único é o que torna a sincronização um upsert em vez de acumular duplicata a
        // cada rodada do catálogo.
        builder.HasIndex(x => new { x.Tipo, x.Valor }).IsUnique().HasDatabaseName("ux_ser_catalogo_recurso");

        builder.HasMany(x => x.Campos)
            .WithOne(x => x.Recurso!)
            .HasForeignKey(x => x.RecursoId)
            .OnDelete(DeleteBehavior.Cascade);
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
