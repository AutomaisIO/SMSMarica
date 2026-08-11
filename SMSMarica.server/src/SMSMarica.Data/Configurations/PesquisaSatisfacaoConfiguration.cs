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
        builder.Property(p => p.UnidadeCnes).HasColumnName("unidade_cnes").HasMaxLength(20);
        builder.Property(p => p.AtendimentoEm).HasColumnName("atendimento_em").IsRequired();
        builder.Property(p => p.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(p => p.ClicadaEm).HasColumnName("clicada_em");
        builder.Property(p => p.Cliques).HasColumnName("cliques").IsRequired().HasDefaultValue(0);
        builder.Property(p => p.WaMessageId).HasColumnName("wa_message_id").HasMaxLength(120);
        builder.Property(p => p.PacienteSexo).HasColumnName("paciente_sexo");
        builder.Property(p => p.PacienteNascimento).HasColumnName("paciente_nascimento");
        builder.Property(p => p.EnviadaEm).HasColumnName("enviada_em");
        builder.Property(p => p.EnviadaPor).HasColumnName("enviada_por");
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.CriadoPor).HasColumnName("criado_por");

        // Uma pesquisa por atendimento — reenviar não pode render duas notas da mesma passagem.
        builder.HasIndex(p => p.EncounterId).IsUnique();

        // Fila de envio e relatório por paciente.
        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.ClicadaEm);
    }
}
