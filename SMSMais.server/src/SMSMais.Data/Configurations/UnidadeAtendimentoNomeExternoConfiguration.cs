using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class UnidadeAtendimentoNomeExternoConfiguration : IEntityTypeConfiguration<UnidadeAtendimentoNomeExterno>
{
    public void Configure(EntityTypeBuilder<UnidadeAtendimentoNomeExterno> builder)
    {
        builder.ToTable("unidade_atendimento_nome_externo");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.UnidadeAtendimentoId).HasColumnName("unidade_atendimento_id").IsRequired();
        builder.Property(n => n.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(n => n.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(300).IsRequired();
        builder.Property(n => n.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(n => n.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(n => n.UnidadeAtendimento)
            .WithMany()
            .HasForeignKey(n => n.UnidadeAtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um nome aponta para UMA unidade: dois endereços para o mesmo nome seria o paciente
        // recebendo um deles ao acaso.
        builder.HasIndex(n => n.NomeNormalizado).IsUnique();
        builder.HasIndex(n => n.UnidadeAtendimentoId);
    }
}
