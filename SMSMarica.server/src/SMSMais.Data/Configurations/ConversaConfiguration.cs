using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Conversas;

namespace SMSMais.Data.Configurations;

internal sealed class ConversaConfiguration : IEntityTypeConfiguration<Conversa>
{
    public void Configure(EntityTypeBuilder<Conversa> builder)
    {
        builder.ToTable("conversa");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Canal).HasColumnName("canal").HasConversion<int>().IsRequired();
        builder.Property(c => c.TelefoneCanonical).HasColumnName("telefone_canonical").HasMaxLength(20).IsRequired();
        builder.Property(c => c.PacienteId).HasColumnName("paciente_id");
        builder.Property(c => c.NomeContato).HasColumnName("nome_contato").HasMaxLength(200);
        builder.Property(c => c.Assunto).HasColumnName("assunto").HasConversion<int>();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(c => c.OperadorResponsavelId).HasColumnName("operador_responsavel_id");
        builder.Property(c => c.UnidadeId).HasColumnName("unidade_id");
        builder.Property(c => c.JanelaExpiraEm).HasColumnName("janela_expira_em");
        builder.Property(c => c.UltimaMensagemEm).HasColumnName("ultima_mensagem_em");
        builder.Property(c => c.UltimaMensagemDirecao).HasColumnName("ultima_mensagem_direcao").HasConversion<int>();
        builder.Property(c => c.UltimaMensagemPreview).HasColumnName("ultima_mensagem_preview").HasMaxLength(200);
        builder.Property(c => c.NaoLidas).HasColumnName("nao_lidas").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.PrimeiroContatoEm).HasColumnName("primeiro_contato_em").IsRequired();

        builder.Property(c => c.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(c => c.CriadoPor).HasColumnName("criado_por");
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(c => c.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(c => c.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(c => c.ExcluidoPor).HasColumnName("excluido_por");

        builder.Property(c => c.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // PacienteId referencia fhir.patient (hub FHIR) — sem FK local.
        builder.HasOne(c => c.OperadorResponsavel)
            .WithMany()
            .HasForeignKey(c => c.OperadorResponsavelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Unidade)
            .WithMany()
            .HasForeignKey(c => c.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        // No máximo uma conversa "viva" (Aberta=1 / Pendente=2) por contato — evita corrida
        // no webhook criando threads duplicadas para o mesmo telefone.
        builder.HasIndex(c => new { c.TelefoneCanonical, c.Canal })
            .IsUnique()
            .HasFilter("status IN (1, 2) AND excluido_em IS NULL");

        builder.HasIndex(c => new { c.UnidadeId, c.UltimaMensagemEm });   // fila da unidade
        builder.HasIndex(c => new { c.OperadorResponsavelId, c.Status }); // "minhas conversas"
        builder.HasIndex(c => c.TelefoneCanonical);                        // roteamento inbound / herança sticky
    }
}
