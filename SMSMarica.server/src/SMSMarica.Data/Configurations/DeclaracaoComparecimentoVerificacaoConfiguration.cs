using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class DeclaracaoComparecimentoVerificacaoConfiguration
    : IEntityTypeConfiguration<DeclaracaoComparecimentoVerificacao>
{
    public void Configure(EntityTypeBuilder<DeclaracaoComparecimentoVerificacao> builder)
    {
        builder.ToTable("declaracao_comparecimento_verificacao");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.ExameImagemId).HasColumnName("exame_imagem_id").IsRequired();
        builder.Property(d => d.DataHoraExame)
            .HasColumnName("data_hora_exame")
            .HasColumnType("timestamp without time zone")
            .IsRequired();
        builder.Property(d => d.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Um selo por solicitação (estável).
        builder.HasIndex(d => d.ExameImagemId).IsUnique();

        builder.HasOne(d => d.ExameImagem)
            .WithMany()
            .HasForeignKey(d => d.ExameImagemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
