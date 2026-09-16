using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.KlinikosWeb;

namespace SMSMais.Data.Configurations.KlinikosWeb;

internal sealed class KlinikosDeepFilaConfiguration : IEntityTypeConfiguration<KlinikosDeepFila>
{
    public void Configure(EntityTypeBuilder<KlinikosDeepFila> builder)
    {
        builder.ToTable("klinikos_deep_fila");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(60).IsRequired();
        builder.Property(x => x.SpaCodigo).HasColumnName("spa_codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.UnidCodigo).HasColumnName("unid_codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Prioridade).HasColumnName("prioridade").IsRequired();

        // Estado como texto (legível no banco, estável a reordenação do enum).
        builder.Property(x => x.Estado)
            .HasColumnName("estado")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Tentativas).HasColumnName("tentativas").IsRequired();
        builder.Property(x => x.UltimaMensagem).HasColumnName("ultima_mensagem").HasMaxLength(2000);
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        // Idempotência: um item por boletim por instância.
        builder.HasIndex(x => new { x.Provedor, x.SpaCodigo })
            .IsUnique()
            .HasDatabaseName("ux_klinikos_deep_provedor_spa");

        // Drenagem: pega o mais prioritário/mais antigo dentre os enfileirados.
        builder.HasIndex(x => new { x.Estado, x.Prioridade, x.CriadoEm })
            .HasDatabaseName("ix_klinikos_deep_drenagem");
    }
}
