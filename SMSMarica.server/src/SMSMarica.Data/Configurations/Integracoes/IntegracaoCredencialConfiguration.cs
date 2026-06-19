using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Integracoes;

namespace SMSMarica.Data.Configurations.Integracoes;

internal sealed class IntegracaoCredencialConfiguration : IEntityTypeConfiguration<IntegracaoCredencial>
{
    public void Configure(EntityTypeBuilder<IntegracaoCredencial> builder)
    {
        builder.ToTable("integracao_credencial");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provedor).HasColumnName("provedor").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Provedor).IsUnique();

        builder.Property(x => x.ClientIdCifrado).HasColumnName("client_id_cifrado");
        builder.Property(x => x.ClientSecretCifrado).HasColumnName("client_secret_cifrado");
        builder.Property(x => x.ParametrosJson).HasColumnName("parametros_json").HasColumnType("jsonb");
        builder.Property(x => x.RedirectUri).HasColumnName("redirect_uri").HasMaxLength(500);
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
    }
}
