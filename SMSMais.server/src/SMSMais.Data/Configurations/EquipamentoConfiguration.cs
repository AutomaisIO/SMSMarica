using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Configurations;

internal sealed class EquipamentoConfiguration : IEntityTypeConfiguration<Equipamento>
{
    public void Configure(EntityTypeBuilder<Equipamento> builder)
    {
        builder.ToTable("equipamento");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(e => e.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(e => e.ModalidadeDicom).HasColumnName("modalidade_dicom").HasConversion<int>().IsRequired();
        builder.Property(e => e.IdentificadorDicom).HasColumnName("identificador_dicom").HasMaxLength(64);
        builder.Property(e => e.DescricaoMaxCaracteres)
            .HasColumnName("descricao_max_caracteres")
            .HasDefaultValue(Equipamento.DescricaoMaxPadrao)
            .IsRequired();
        // Nasce 0 (não envia): ligar o código do procedimento é decisão aparelho a aparelho.
        builder.Property(e => e.CodigoProcedimentoDestinos)
            .HasColumnName("codigo_procedimento_destinos")
            .HasConversion<int>()
            .HasDefaultValue(DestinoCodigoProcedimento.Nenhum)
            .IsRequired();
        builder.Property(e => e.CodigoProcedimentoEsquema)
            .HasColumnName("codigo_procedimento_esquema").HasMaxLength(16);
        // Campos opcionais (adendo do ADR-0074): tudo nasce nulo = o item sai como sempre saiu.
        builder.Property(e => e.CodigoProcedimentoTag)
            .HasColumnName("codigo_procedimento_tag").HasMaxLength(8);
        builder.Property(e => e.MedicoSolicitanteTag)
            .HasColumnName("medico_solicitante_tag").HasMaxLength(8);
        builder.Property(e => e.UnidadeTag)
            .HasColumnName("unidade_tag").HasMaxLength(8);
        builder.Property(e => e.UnidadeOrigem)
            .HasColumnName("unidade_origem")
            .HasConversion<int>()
            .HasDefaultValue(OrigemUnidadeWorklist.Executante)
            .IsRequired();
        // O que acontece com o exame depois que chega (ADR-0075). O padrão (laudo na plataforma,
        // retido até o laudo, com anamnese) vem da entidade, e não do banco: com default no banco o
        // EF omite do INSERT o valor igual ao default do CLR — o false de UsaAnamnese — e o aparelho
        // cadastrado sem anamnese nasceria com ela. O valor dos aparelhos que já existiam está na
        // migration.
        builder.Property(e => e.Laudo)
            .HasColumnName("origem_laudo")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(e => e.LiberacaoPaciente)
            .HasColumnName("liberacao_paciente")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(e => e.UsaAnamnese)
            .HasColumnName("usa_anamnese")
            .IsRequired();
        builder.Property(e => e.Ativo).HasColumnName("ativo").IsRequired();

        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.CriadoPor).HasColumnName("criado_por");
        builder.Property(e => e.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(e => e.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(e => e.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(e => e.ExcluidoPor).HasColumnName("excluido_por");

        builder.HasOne(e => e.Unidade)
            .WithMany()
            .HasForeignKey(e => e.UnidadeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UnidadeId, e.Nome })
            .HasDatabaseName("ux_equipamento_unidade_nome")
            .IsUnique()
            .HasFilter("excluido_em IS NULL");

        // Fora da faixa não existe aparelho: abaixo de 4 nenhuma descrição sobrevive, acima de 64
        // o próprio DICOM corta (VR LO). A trava é no banco para o valor não entrar por script.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_equipamento_descricao_max",
            $"descricao_max_caracteres BETWEEN 4 AND {Equipamento.DescricaoMaxPadrao}"));

        // "Só com o laudo assinado" com o laudo feito fora prenderia o exame para sempre: não há
        // laudo nosso para soltá-lo. Trava no banco, como a de cima, para não entrar por script.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_equipamento_liberacao_com_laudo",
            $"liberacao_paciente <> {(int)LiberacaoExamePaciente.ComLaudoAssinado} " +
            $"OR origem_laudo = {(int)OrigemLaudo.Plataforma}"));

        builder.HasIndex(e => e.ExcluidoEm)
            .HasDatabaseName("ix_equipamento_excluido_em")
            .HasFilter("excluido_em IS NULL");
    }
}
