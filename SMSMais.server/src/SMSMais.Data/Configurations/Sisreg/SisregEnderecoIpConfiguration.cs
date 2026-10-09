using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregEnderecoIpConfiguration : IEntityTypeConfiguration<SisregEnderecoIp>
{
    public void Configure(EntityTypeBuilder<SisregEnderecoIp> builder)
    {
        builder.ToTable("sisreg_endereco_ip");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        // 45 = maior texto de IPv6; hoje só IPv4 é resolvido.
        builder.Property(x => x.Ip).HasColumnName("ip").HasMaxLength(45).IsRequired();
        builder.Property(x => x.DesdeEm).HasColumnName("desde_em").IsRequired();
        builder.Property(x => x.UltimaVezVistoEm).HasColumnName("ultima_vez_visto_em").IsRequired();
        builder.Property(x => x.AteEm).HasColumnName("ate_em");
        builder.Property(x => x.InterfaceRota).HasColumnName("interface_rota").HasMaxLength(40);

        // Um período ABERTO por IP: o mesmo IP pode voltar depois (período novo), mas nunca dois abertos.
        builder.HasIndex(x => x.Ip)
            .HasDatabaseName("ux_sisreg_endereco_ip_aberto")
            .IsUnique()
            .HasFilter("ate_em IS NULL");
    }
}
