using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(u => u.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(u => u.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(u => u.FotoBase64).HasColumnName("foto_base64").HasColumnType("text");
        builder.Property(u => u.SenhaHash).HasColumnName("senha_hash").HasMaxLength(500).IsRequired();
        builder.Property(u => u.Ativo).HasColumnName("ativo").HasDefaultValue(true).IsRequired();
        builder.Property(u => u.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(u => u.UltimoAcessoEm).HasColumnName("ultimo_acesso_em");

        builder.OwnsOne(u => u.Endereco, e =>
        {
            e.Property(x => x.Cep).HasColumnName("endereco_cep").HasMaxLength(8);
            e.Property(x => x.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            e.Property(x => x.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            e.Property(x => x.Complemento).HasColumnName("endereco_complemento").HasMaxLength(120);
            e.Property(x => x.Bairro).HasColumnName("endereco_bairro").HasMaxLength(120);
            e.Property(x => x.Cidade).HasColumnName("endereco_cidade").HasMaxLength(120);
            e.Property(x => x.Uf).HasColumnName("endereco_uf").HasMaxLength(2);
            e.Property(x => x.PontoReferencia).HasColumnName("endereco_ponto_referencia").HasMaxLength(200);
        });

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
