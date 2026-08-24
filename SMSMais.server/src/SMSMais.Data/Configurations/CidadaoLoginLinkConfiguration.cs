using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoLoginLinkConfiguration : IEntityTypeConfiguration<CidadaoLoginLink>
{
    public void Configure(EntityTypeBuilder<CidadaoLoginLink> builder)
    {
        builder.ToTable("cidadao_login_link");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(l => l.Cpf).HasColumnName("cpf").HasMaxLength(11).IsRequired();
        builder.Property(l => l.Destino).HasColumnName("destino").HasMaxLength(200);
        // Sem FK: link é histórico/auditoria — sobrevive à exclusão da solicitação.
        builder.Property(l => l.SolicitacaoId).HasColumnName("solicitacao_id");
        builder.Property(l => l.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(l => l.ExigeConfirmacaoCpf)
            .HasColumnName("exige_confirmacao_cpf").HasDefaultValue(false).IsRequired();
        builder.Property(l => l.TentativasCpf)
            .HasColumnName("tentativas_cpf").HasDefaultValue(0).IsRequired();
        builder.Property(l => l.UsadoEm).HasColumnName("usado_em");
        builder.Property(l => l.UsadoIp).HasColumnName("usado_ip").HasMaxLength(64);
        builder.Property(l => l.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(l => l.CriadoPor).HasColumnName("criado_por");

        builder.HasIndex(l => l.PatientId);
    }
}
