using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class TipoTratamentoConfiguration : IEntityTypeConfiguration<TipoTratamento>
{
    // IDs fixos para o seed (estáveis entre migrations e ambientes).
    public static readonly Guid HemodialiseId = new("0193d000-0000-7000-a000-000000000001");
    public static readonly Guid RadioterapiaId = new("0193d000-0000-7000-a000-000000000002");

    public void Configure(EntityTypeBuilder<TipoTratamento> builder)
    {
        builder.ToTable("tipo_tratamento");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Nome).HasColumnName("nome").HasMaxLength(120).IsRequired();
        builder.Property(t => t.Codigo).HasColumnName("codigo").HasMaxLength(60).IsRequired();
        builder.Property(t => t.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em").IsRequired();

        builder.HasIndex(t => t.Codigo).IsUnique();

        var semente = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new TipoTratamento { Id = HemodialiseId, Nome = "Hemodiálise", Codigo = "hemodialise", Ativo = true, CriadoEm = semente },
            new TipoTratamento { Id = RadioterapiaId, Nome = "Radioterapia", Codigo = "radioterapia", Ativo = true, CriadoEm = semente });
    }
}
