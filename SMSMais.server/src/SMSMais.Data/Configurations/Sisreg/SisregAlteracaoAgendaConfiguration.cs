using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregAlteracaoAgendaConfiguration : IEntityTypeConfiguration<SisregAlteracaoAgenda>
{
    public void Configure(EntityTypeBuilder<SisregAlteracaoAgenda> builder)
    {
        builder.ToTable("sisreg_alteracao_agenda");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.CodigoSolicitacao).HasColumnName("codigo_solicitacao").HasMaxLength(50);
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>().IsRequired();
        builder.Property(x => x.ValorAntes).HasColumnName("valor_antes").HasMaxLength(300);
        builder.Property(x => x.ValorDepois).HasColumnName("valor_depois").HasMaxLength(300);
        builder.Property(x => x.UnidadeExecutanteId).HasColumnName("unidade_executante_id");
        builder.Property(x => x.UnidadeSolicitanteId).HasColumnName("unidade_solicitante_id");
        builder.Property(x => x.DetectadaEm).HasColumnName("detectada_em").IsRequired();
        builder.Property(x => x.TratadaEm).HasColumnName("tratada_em");
        builder.Property(x => x.TratadaPor).HasColumnName("tratada_por");
        builder.Property(x => x.ComunicadaEm).HasColumnName("comunicada_em");
        builder.Property(x => x.ComunicadaPor).HasColumnName("comunicada_por");

        builder.HasOne(x => x.Solicitacao)
            .WithMany()
            .HasForeignKey(x => x.SolicitacaoId)
            // Solicitação apagada leva o rastro junto: alteração sem solicitação não tem o que
            // resolver, e manter órfã só sujaria a fila do regulador.
            .OnDelete(DeleteBehavior.Cascade);

        // A tela abre sempre na fila do que falta tratar, mais recente primeiro.
        builder.HasIndex(x => new { x.TratadaEm, x.DetectadaEm });
        builder.HasIndex(x => x.UnidadeExecutanteId);
        builder.HasIndex(x => x.SolicitacaoId);
    }
}
