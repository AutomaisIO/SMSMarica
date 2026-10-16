using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

/// <summary>Itens de procedimento de uma solicitação (ticket #149). Tamanhos espelham os campos
/// equivalentes da espinha (<see cref="SolicitacaoConfiguration"/>).</summary>
internal sealed class SolicitacaoProcedimentoConfiguration : IEntityTypeConfiguration<SolicitacaoProcedimento>
{
    public void Configure(EntityTypeBuilder<SolicitacaoProcedimento> builder)
    {
        builder.ToTable("solicitacao_procedimento");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SolicitacaoId).HasColumnName("solicitacao_id").IsRequired();
        builder.Property(x => x.Ordem).HasColumnName("ordem").IsRequired();

        builder.Property(x => x.ProcedimentoCodigoSisreg).HasColumnName("procedimento_codigo_sisreg").HasMaxLength(20);
        builder.Property(x => x.ProcedimentoSigtapCodigo).HasColumnName("procedimento_sigtap_codigo").HasMaxLength(20);
        builder.Property(x => x.ProcedimentoTexto).HasColumnName("procedimento_texto").HasMaxLength(300);
        builder.Property(x => x.ProfissionalExecutanteCpf).HasColumnName("profissional_executante_cpf").HasMaxLength(11);
        builder.Property(x => x.ProfissionalExecutanteNome).HasColumnName("profissional_executante_nome").HasMaxLength(200);
        builder.Property(x => x.DataAgendada).HasColumnName("data_agendada");
        builder.Property(x => x.RawSisreg).HasColumnName("raw_sisreg");

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(x => x.Solicitacao)
            .WithMany()
            .HasForeignKey(x => x.SolicitacaoId)
            // Item não existe sem o pedido — mesma régua de sisreg_alteracao_agenda.
            .OnDelete(DeleteBehavior.Cascade);

        // Uma posição por pedido entre os itens vivos: é o que mantém o "principal" único.
        builder.HasIndex(x => new { x.SolicitacaoId, x.Ordem })
            .IsUnique()
            .HasFilter("excluido_em IS NULL");
    }
}
