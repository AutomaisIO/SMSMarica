using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CompartilhamentoLocalizacaoConfiguration : IEntityTypeConfiguration<CompartilhamentoLocalizacao>
{
    public void Configure(EntityTypeBuilder<CompartilhamentoLocalizacao> builder)
    {
        builder.ToTable("translado_compartilhamento_localizacao");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.SessaoId).HasColumnName("sessao_id").IsRequired();
        builder.Property(c => c.Trecho).HasColumnName("trecho").HasConversion<int>().IsRequired();
        builder.Property(c => c.CidadaoSessaoId).HasColumnName("cidadao_sessao_id").IsRequired();
        builder.Property(c => c.Canal).HasColumnName("canal").HasMaxLength(16).IsRequired();
        builder.Property(c => c.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(c => c.EncerradoEm).HasColumnName("encerrado_em");
        builder.Property(c => c.MotivoEncerramento).HasColumnName("motivo_encerramento").HasConversion<int?>();
        builder.Property(c => c.Latitude).HasColumnName("latitude");
        builder.Property(c => c.Longitude).HasColumnName("longitude");
        builder.Property(c => c.PrecisaoM).HasColumnName("precisao_m");
        builder.Property(c => c.Simulada).HasColumnName("simulada").IsRequired();
        builder.Property(c => c.CapturadaEm).HasColumnName("capturada_em");
        builder.Property(c => c.RecebidaEm).HasColumnName("recebida_em");

        builder.HasOne<SessaoDeTratamento>().WithMany().HasForeignKey(c => c.SessaoId).OnDelete(DeleteBehavior.Cascade);

        // Um compartilhamento por sessão e trecho: parar e voltar reabre a mesma linha.
        builder.HasIndex(c => new { c.SessaoId, c.Trecho }).IsUnique();
        // A rotina que encerra quem saiu da espera só olha os abertos.
        builder.HasIndex(c => c.EncerradoEm);
    }
}
