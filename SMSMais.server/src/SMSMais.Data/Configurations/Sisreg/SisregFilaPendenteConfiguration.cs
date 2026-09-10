using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregFilaPendenteConfiguration : IEntityTypeConfiguration<SisregFilaPendente>
{
    public void Configure(EntityTypeBuilder<SisregFilaPendente> builder)
    {
        builder.ToTable("sisreg_fila_pendente");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoSolicitacao)
            .HasColumnName("codigo_solicitacao").HasMaxLength(20).IsRequired();

        builder.Property(x => x.DataSolicitacao).HasColumnName("data_solicitacao");
        builder.Property(x => x.Risco).HasColumnName("risco");

        builder.Property(x => x.PacienteNome).HasColumnName("paciente_nome").HasMaxLength(200);
        builder.Property(x => x.Cns).HasColumnName("cns").HasMaxLength(15);
        builder.Property(x => x.NomeMae).HasColumnName("nome_mae").HasMaxLength(200);
        builder.Property(x => x.DataNascimento).HasColumnName("data_nascimento");
        builder.Property(x => x.IdadeAnos).HasColumnName("idade_anos");
        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(120);
        builder.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(120);

        builder.Property(x => x.ProcedimentoNome).HasColumnName("procedimento_nome").HasMaxLength(300);
        builder.Property(x => x.ProcedimentoCodigo).HasColumnName("procedimento_codigo").HasMaxLength(20);
        builder.Property(x => x.CidCodigo).HasColumnName("cid_codigo").HasMaxLength(20);
        builder.Property(x => x.UnidadeSolicitante).HasColumnName("unidade_solicitante").HasMaxLength(200);
        builder.Property(x => x.Situacao).HasColumnName("situacao").HasMaxLength(60);

        builder.Property(x => x.PrimeiroVistoEm).HasColumnName("primeiro_visto_em").IsRequired();
        builder.Property(x => x.UltimoVistoEm).HasColumnName("ultimo_visto_em").IsRequired();
        builder.Property(x => x.SaiuEm).HasColumnName("saiu_em");
        builder.Property(x => x.SaiuPara).HasColumnName("saiu_para").HasConversion<int>();

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");

        // O número da solicitação é único no SISREG e é a chave do upsert: a mesma pessoa relida
        // todo dia tem de ATUALIZAR a linha, não criar outra. Sem isto, uma fila de 15 mil
        // relida diariamente viraria meio milhão de linhas em um mês.
        builder.HasIndex(x => x.CodigoSolicitacao)
            .IsUnique()
            .HasDatabaseName("ix_sisreg_fila_pendente_codigo");

        // A pergunta que a tela faz o tempo todo: "quem ainda espera por este procedimento".
        // Índice parcial porque quem já saiu não é fila — e a parte que interessa encolhe.
        builder.HasIndex(x => new { x.ProcedimentoNome, x.DataSolicitacao })
            .HasFilter("saiu_em IS NULL")
            .HasDatabaseName("ix_sisreg_fila_pendente_aberta_por_procedimento");

        // Casar a fila com o paciente do hub, e achar a pessoa pelo cartão.
        builder.HasIndex(x => x.Cns)
            .HasFilter("cns IS NOT NULL AND saiu_em IS NULL")
            .HasDatabaseName("ix_sisreg_fila_pendente_cns_aberta");
    }
}
