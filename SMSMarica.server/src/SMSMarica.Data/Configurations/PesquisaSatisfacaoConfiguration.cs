using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class PesquisaSatisfacaoConfiguration : IEntityTypeConfiguration<PesquisaSatisfacao>
{
    public void Configure(EntityTypeBuilder<PesquisaSatisfacao> builder)
    {
        builder.ToTable("pesquisa_satisfacao");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(p => p.EncounterId).HasColumnName("encounter_id").IsRequired();
        builder.Property(p => p.UnidadeNome).HasColumnName("unidade_nome").HasMaxLength(200);
        builder.Property(p => p.AtendimentoEm).HasColumnName("atendimento_em").IsRequired();
        builder.Property(p => p.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(p => p.InstrumentoVersao)
            .HasColumnName("instrumento_versao").HasMaxLength(60).IsRequired();
        builder.Property(p => p.RespostasJson).HasColumnName("respostas").HasColumnType("jsonb");
        builder.Property(p => p.RespondidaEm).HasColumnName("respondida_em");
        builder.Property(p => p.RespondidaIp).HasColumnName("respondida_ip").HasMaxLength(64);
        builder.Property(p => p.EnviadaEm).HasColumnName("enviada_em");
        builder.Property(p => p.EnviadaPor).HasColumnName("enviada_por");
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.CriadoPor).HasColumnName("criado_por");

        // Uma pesquisa por atendimento — reenviar não pode render duas notas da mesma passagem.
        builder.HasIndex(p => p.EncounterId).IsUnique();

        // Fila de envio e relatório por paciente.
        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.RespondidaEm);
    }
}
