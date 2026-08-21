using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Automais.Zap.Data.Configurations;

public sealed class NumeroConfiguration : IEntityTypeConfiguration<Numero>
{
    public void Configure(EntityTypeBuilder<Numero> b)
    {
        b.ToTable("numero");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PhoneNumberId).HasColumnName("phone_number_id").HasMaxLength(60).IsRequired();
        b.Property(x => x.WabaId).HasColumnName("waba_id");
        b.Property(x => x.DisplayPhoneNumber).HasColumnName("display_phone_number").HasMaxLength(40);
        b.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(200);
        b.Property(x => x.UrlDestinoOverride).HasColumnName("url_destino_override").HasMaxLength(500);
        b.Property(x => x.Ativo).HasColumnName("ativo").HasDefaultValue(true);
        b.Property(x => x.CriadoEm).HasColumnName("criado_em");
        b.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");

        // O roteamento inteiro depende deste índice ser único: dois tenants reivindicando o
        // mesmo phone_number_id seria mensagem de um município caindo em outro.
        b.HasIndex(x => x.PhoneNumberId).IsUnique().HasDatabaseName("ux_numero_phone_number_id");

        b.HasOne(x => x.Waba).WithMany(w => w.Numeros)
            .HasForeignKey(x => x.WabaId).OnDelete(DeleteBehavior.Cascade);
    }
}
