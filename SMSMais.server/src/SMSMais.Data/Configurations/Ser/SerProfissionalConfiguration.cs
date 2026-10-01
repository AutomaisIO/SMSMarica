using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Data.Configurations.Ser;

internal sealed class SerProfissionalConfiguration : IEntityTypeConfiguration<SerProfissional>
{
    public void Configure(EntityTypeBuilder<SerProfissional> builder)
    {
        builder.ToTable("ser_profissional");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Chave).HasColumnName("chave").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.NomeNormalizado).HasColumnName("nome_normalizado").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cpf).HasColumnName("cpf").HasMaxLength(11);
        builder.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(40);
        builder.Property(x => x.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(20);
        builder.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
        builder.Property(x => x.Ocorrencias).HasColumnName("ocorrencias").IsRequired().HasDefaultValue(1);
        builder.Property(x => x.PresenteNoSer).HasColumnName("presente_no_ser").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.PrimeiraLeituraEm).HasColumnName("primeira_leitura_em").IsRequired();
        builder.Property(x => x.UltimaLeituraEm).HasColumnName("ultima_leitura_em").IsRequired();
        builder.Property(x => x.MedicoId).HasColumnName("medico_id");
        builder.Property(x => x.MedicoNome).HasColumnName("medico_nome").HasMaxLength(200);
        builder.Property(x => x.LigadoEm).HasColumnName("ligado_em");
        builder.Property(x => x.LigadoPor).HasColumnName("ligado_por");

        builder.HasIndex(x => x.Chave).IsUnique().HasDatabaseName("ux_ser_profissional_chave");
        builder.HasIndex(x => x.Cpf).HasDatabaseName("ix_ser_profissional_cpf");
        builder.HasIndex(x => x.MedicoId).HasDatabaseName("ix_ser_profissional_medico");
    }
}
