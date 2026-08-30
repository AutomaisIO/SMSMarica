using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregMapeamentoLoteExecucaoItemConfiguration
    : IEntityTypeConfiguration<SisregMapeamentoLoteExecucaoItem>
{
    public void Configure(EntityTypeBuilder<SisregMapeamentoLoteExecucaoItem> builder)
    {
        builder.ToTable("sisreg_mapeamento_lote_execucao_item");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ExecucaoId).HasColumnName("execucao_id").IsRequired();
        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.UnidadeNome).HasColumnName("unidade_nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Cnes).HasColumnName("cnes").HasMaxLength(7);
        builder.Property(x => x.UnidadeCriada).HasColumnName("unidade_criada").IsRequired();
        builder.Property(x => x.Resultado).HasColumnName("resultado").IsRequired();

        builder.Property(x => x.ProfissionaisEncontrados).HasColumnName("profissionais_encontrados").IsRequired();
        builder.Property(x => x.ProfissionaisNovos).HasColumnName("profissionais_novos").IsRequired();
        builder.Property(x => x.ProfissionaisAusentes).HasColumnName("profissionais_ausentes").IsRequired();
        builder.Property(x => x.ProcedimentosEncontrados).HasColumnName("procedimentos_encontrados").IsRequired();
        builder.Property(x => x.ProcedimentosNovos).HasColumnName("procedimentos_novos").IsRequired();
        builder.Property(x => x.PractitionersCriados).HasColumnName("practitioners_criados").IsRequired();
        builder.Property(x => x.PractitionersVinculados).HasColumnName("practitioners_vinculados").IsRequired();
        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();

        builder.Property(x => x.Observacao).HasColumnName("observacao").HasMaxLength(1000);
        builder.Property(x => x.RegistradoEm).HasColumnName("registrado_em").IsRequired();

        // Cai junto com a execução: o detalhe não faz sentido sem o pai.
        builder.HasOne(x => x.Execucao)
            .WithMany()
            .HasForeignKey(x => x.ExecucaoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sem FK para unidade: o rastreio sobrevive à exclusão do cadastro (o nome fica
        // desnormalizado justamente para isso), igual ao rastreio da varredura.
        builder.HasIndex(x => x.ExecucaoId)
            .HasDatabaseName("ix_sisreg_mapeamento_lote_execucao_item_execucao");
    }
}
