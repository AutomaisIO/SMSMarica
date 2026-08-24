using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregVarreduraExecucaoItemConfiguration : IEntityTypeConfiguration<SisregVarreduraExecucaoItem>
{
    public void Configure(EntityTypeBuilder<SisregVarreduraExecucaoItem> builder)
    {
        builder.ToTable("sisreg_varredura_execucao_item");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id").IsRequired();
        builder.Property(x => x.ProfissionalCpf).HasColumnName("profissional_cpf").HasMaxLength(11).IsRequired();
        builder.Property(x => x.ProfissionalNome).HasColumnName("profissional_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProcedimentoCodigo).HasColumnName("procedimento_codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProcedimentoNome).HasColumnName("procedimento_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();
        builder.Property(x => x.RegistrosEncontrados).HasColumnName("registros_encontrados").IsRequired();
        builder.Property(x => x.Validos).HasColumnName("validos").IsRequired();
        builder.Property(x => x.Invalidos).HasColumnName("invalidos").IsRequired();
        builder.Property(x => x.JaExistiam).HasColumnName("ja_existiam").IsRequired();
        builder.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(500);

        // Cai junto com a execução: o detalhe não faz sentido sem o pai, e nenhuma outra tabela o
        // referencia.
        builder.HasOne(x => x.Execucao)
            .WithMany()
            .HasForeignKey(x => x.ExecucaoId)
            .OnDelete(DeleteBehavior.Cascade);

        // O modal lê "os itens desta execução".
        builder.HasIndex(x => x.ExecucaoId)
            .HasDatabaseName("ix_sisreg_varredura_execucao_item_execucao");
    }
}
