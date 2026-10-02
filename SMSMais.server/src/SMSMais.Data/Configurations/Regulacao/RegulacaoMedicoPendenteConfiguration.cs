using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoMedicoPendenteConfiguration : IEntityTypeConfiguration<RegulacaoMedicoPendente>
{
    public void Configure(EntityTypeBuilder<RegulacaoMedicoPendente> builder)
    {
        builder.ToTable("regulacao_medico_pendente");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(10);
        builder.Property(x => x.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(40);
        builder.Property(x => x.Especialidade).HasColumnName("especialidade").HasMaxLength(200);
        builder.Property(x => x.Situacao).HasColumnName("situacao").IsRequired();
        builder.Property(x => x.NomeNoSistema).HasColumnName("nome_no_sistema").HasMaxLength(300);
        builder.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(1000);
        builder.Property(x => x.ResolvidoEm).HasColumnName("resolvido_em");
        builder.Property(x => x.ResolvidoPor).HasColumnName("resolvido_por");
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        // A fila do técnico: os pendentes de um sistema.
        builder.HasIndex(x => new { x.Sistema, x.Situacao })
            .HasDatabaseName("ix_regulacao_medico_pendente_sistema_situacao");
    }
}
