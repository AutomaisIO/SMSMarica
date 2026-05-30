using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class MotoristaConfiguration : IEntityTypeConfiguration<Motorista>
{
    public void Configure(EntityTypeBuilder<Motorista> builder)
    {
        builder.ToTable("motorista");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");

        // Identidade inline (Fatia 4)
        builder.Property(m => m.NomeCompleto).HasColumnName("nome_completo").HasMaxLength(200).IsRequired();
        builder.Property(m => m.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(m => m.Rg).HasColumnName("rg").HasMaxLength(20);
        builder.Property(m => m.DataNascimento).HasColumnName("data_nascimento");
        builder.Property(m => m.Sexo).HasColumnName("sexo").HasConversion<int?>();
        builder.Property(m => m.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(m => m.FotoBase64).HasColumnName("foto_base64").HasColumnType("text");

        builder.Property(m => m.Cnh).HasColumnName("cnh").HasMaxLength(11).IsRequired();

        // Auditoria
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(m => m.CriadoPor).HasColumnName("criado_por");
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(m => m.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(m => m.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(m => m.ExcluidoPor).HasColumnName("excluido_por");

        builder.OwnsOne(m => m.Endereco, e =>
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

        builder.HasIndex(m => m.Cnh).IsUnique();
        builder.HasIndex(m => m.Cpf)
            .IsUnique()
            .HasFilter("cpf IS NOT NULL");
        builder.HasIndex(m => m.ExcluidoEm)
            .HasDatabaseName("ix_motorista_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
