using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Configurations.Regulacao;

internal sealed class RegulacaoAnaliseEspelhoConfiguration : IEntityTypeConfiguration<RegulacaoAnaliseEspelho>
{
    public void Configure(EntityTypeBuilder<RegulacaoAnaliseEspelho> builder)
    {
        builder.ToTable("regulacao_analise_espelho");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Sistema).HasColumnName("sistema").IsRequired();
        builder.Property(x => x.EspelhoId).HasColumnName("espelho_id").IsRequired();
        builder.Property(x => x.NumeroExterno).HasColumnName("numero_externo").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProcedimentoId).HasColumnName("procedimento_id");
        builder.Property(x => x.PacienteId).HasColumnName("paciente_id");
        builder.Property(x => x.Veredito).HasColumnName("veredito").IsRequired();
        builder.Property(x => x.Bloqueios).HasColumnName("bloqueios").IsRequired();
        builder.Property(x => x.Ressalvas).HasColumnName("ressalvas").IsRequired();
        builder.Property(x => x.Avisos).HasColumnName("avisos").IsRequired();
        builder.Property(x => x.PerguntasPendentes).HasColumnName("perguntas_pendentes").IsRequired();
        builder.Property(x => x.DocumentosPendentes).HasColumnName("documentos_pendentes").IsRequired();
        builder.Property(x => x.Resumo).HasColumnName("resumo").HasMaxLength(500);
        builder.Property(x => x.AlertasJson).HasColumnName("alertas_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.EntradaHash).HasColumnName("entrada_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AnalisadoEm).HasColumnName("analisado_em").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

        // Uma análise por pedido de espelho — sobrescrita quando a entrada muda.
        builder.HasIndex(x => new { x.Sistema, x.EspelhoId })
            .IsUnique()
            .HasDatabaseName("ux_regulacao_analise_espelho_sistema_espelho");

        builder.HasIndex(x => new { x.Sistema, x.Veredito })
            .HasDatabaseName("ix_regulacao_analise_espelho_sistema_veredito");

        builder.HasOne<RegulacaoProcedimento>()
            .WithMany()
            .HasForeignKey(x => x.ProcedimentoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
