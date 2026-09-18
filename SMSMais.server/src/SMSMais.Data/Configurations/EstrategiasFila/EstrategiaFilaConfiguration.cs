using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.EstrategiasFila;

namespace SMSMais.Data.Configurations.EstrategiasFila;

internal sealed class EstrategiaFilaConfiguration : IEntityTypeConfiguration<EstrategiaFila>
{
    public void Configure(EntityTypeBuilder<EstrategiaFila> builder)
    {
        builder.ToTable("estrategia_fila");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProcedimentoCodigo).HasColumnName("procedimento_codigo").HasMaxLength(20);
        builder.Property(x => x.ProcedimentoNome).HasColumnName("procedimento_nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.RegulacaoProcedimentoId).HasColumnName("regulacao_procedimento_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.ParametrosJson).HasColumnName("parametros_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RodadaAtualId).HasColumnName("rodada_atual_id");

        builder.Property(x => x.AplicadaEm).HasColumnName("aplicada_em");
        builder.Property(x => x.AplicadaPor).HasColumnName("aplicada_por");
        builder.Property(x => x.AplicacaoNota).HasColumnName("aplicacao_nota").HasMaxLength(2000);

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");
        builder.Property(x => x.ExcluidoEm).HasColumnName("excluido_em");
        builder.Property(x => x.ExcluidoPor).HasColumnName("excluido_por");

        // Só para exibir o nome canônico; se o catálogo apagar o canônico, a estratégia fica.
        builder.HasOne(x => x.RegulacaoProcedimento)
            .WithMany()
            .HasForeignKey(x => x.RegulacaoProcedimentoId)
            .OnDelete(DeleteBehavior.SetNull);

        // "Já existe estratégia para este procedimento?" — a lista de procedimentos pergunta isso
        // para cada linha.
        builder.HasIndex(x => x.ProcedimentoCodigo)
            .HasDatabaseName("ix_estrategia_fila_procedimento_codigo");
        builder.HasIndex(x => x.ProcedimentoNome)
            .HasDatabaseName("ix_estrategia_fila_procedimento_nome");
        builder.HasIndex(x => x.Status)
            .HasFilter("excluido_em IS NULL")
            .HasDatabaseName("ix_estrategia_fila_status_viva");
    }
}

internal sealed class EstrategiaFilaRodadaConfiguration : IEntityTypeConfiguration<EstrategiaFilaRodada>
{
    public void Configure(EntityTypeBuilder<EstrategiaFilaRodada> builder)
    {
        builder.ToTable("estrategia_fila_rodada");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EstrategiaId).HasColumnName("estrategia_id").IsRequired();
        builder.Property(x => x.Numero).HasColumnName("numero").IsRequired();
        builder.Property(x => x.Modo).HasColumnName("modo").HasConversion<int>().IsRequired();

        builder.Property(x => x.ParametrosEntradaJson).HasColumnName("parametros_entrada_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CenarioJson).HasColumnName("cenario_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ParametrosResultadoJson).HasColumnName("parametros_resultado_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ProjecaoJson).HasColumnName("projecao_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PropostaJson).HasColumnName("proposta_json").HasColumnType("jsonb");

        builder.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(80);
        builder.Property(x => x.TokensEntrada).HasColumnName("tokens_entrada").IsRequired();
        builder.Property(x => x.TokensSaida).HasColumnName("tokens_saida").IsRequired();
        builder.Property(x => x.CustoUsd).HasColumnName("custo_usd").HasPrecision(12, 6);
        builder.Property(x => x.DuracaoMs).HasColumnName("duracao_ms").IsRequired();
        builder.Property(x => x.Falha).HasColumnName("falha").HasMaxLength(2000);

        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");

        builder.HasOne(x => x.Estrategia)
            .WithMany(x => x.Rodadas)
            .HasForeignKey(x => x.EstrategiaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Numeração é por estratégia e nunca se repete — é o que o histórico e o "comparar" usam.
        builder.HasIndex(x => new { x.EstrategiaId, x.Numero })
            .IsUnique()
            .HasDatabaseName("ux_estrategia_fila_rodada_numero");
    }
}
