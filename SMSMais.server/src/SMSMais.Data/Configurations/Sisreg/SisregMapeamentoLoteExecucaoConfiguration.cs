using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregMapeamentoLoteExecucaoConfiguration
    : IEntityTypeConfiguration<SisregMapeamentoLoteExecucao>
{
    public void Configure(EntityTypeBuilder<SisregMapeamentoLoteExecucao> builder)
    {
        builder.ToTable("sisreg_mapeamento_lote_execucao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Disparo).HasColumnName("disparo").IsRequired()
            .HasDefaultValue(DisparoSincronizacao.Manual);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();

        builder.Property(x => x.UnidadesNoSisreg).HasColumnName("unidades_no_sisreg").IsRequired();
        builder.Property(x => x.UnidadesCriadas).HasColumnName("unidades_criadas").IsRequired();
        builder.Property(x => x.UnidadesComCnesPreenchido).HasColumnName("unidades_com_cnes_preenchido").IsRequired();
        builder.Property(x => x.UnidadesTotal).HasColumnName("unidades_total").IsRequired();
        builder.Property(x => x.UnidadesMapeadas).HasColumnName("unidades_mapeadas").IsRequired();
        builder.Property(x => x.UnidadesPuladas).HasColumnName("unidades_puladas").IsRequired();
        builder.Property(x => x.UnidadesComErro).HasColumnName("unidades_com_erro").IsRequired();

        builder.Property(x => x.ProfissionaisEncontrados).HasColumnName("profissionais_encontrados").IsRequired();
        builder.Property(x => x.ProfissionaisNovos).HasColumnName("profissionais_novos").IsRequired();
        builder.Property(x => x.ProcedimentosEncontrados).HasColumnName("procedimentos_encontrados").IsRequired();
        builder.Property(x => x.ProcedimentosNovos).HasColumnName("procedimentos_novos").IsRequired();
        builder.Property(x => x.PractitionersCriados).HasColumnName("practitioners_criados").IsRequired();
        builder.Property(x => x.PractitionersVinculados).HasColumnName("practitioners_vinculados").IsRequired();
        builder.Property(x => x.Requisicoes).HasColumnName("requisicoes").IsRequired();

        builder.Property(x => x.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(2000);
        builder.Property(x => x.IniciadoEm).HasColumnName("iniciado_em").IsRequired();
        builder.Property(x => x.FinalizadoEm).HasColumnName("finalizado_em");
        builder.Property(x => x.DuracaoSegundos).HasColumnName("duracao_segundos");
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        // A tela lista "as sincronizações recentes", mais nova primeiro.
        builder.HasIndex(x => x.IniciadoEm)
            .HasDatabaseName("ix_sisreg_mapeamento_lote_execucao_iniciado")
            .IsDescending(true);
    }
}
