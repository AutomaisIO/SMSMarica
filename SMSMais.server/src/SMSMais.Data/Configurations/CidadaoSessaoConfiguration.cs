using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class CidadaoSessaoConfiguration : IEntityTypeConfiguration<CidadaoSessao>
{
    public void Configure(EntityTypeBuilder<CidadaoSessao> builder)
    {
        builder.ToTable("cidadao_sessao");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CidadaoAcessoId).HasColumnName("cidadao_acesso_id").IsRequired();
        builder.Property(s => s.Canal).HasColumnName("canal").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Dispositivo).HasColumnName("dispositivo").HasMaxLength(255);
        builder.Property(s => s.Ip).HasColumnName("ip").HasMaxLength(64);
        builder.Property(s => s.CriadaEm).HasColumnName("criada_em").IsRequired();
        builder.Property(s => s.ExpiraEm).HasColumnName("expira_em").IsRequired();
        builder.Property(s => s.RevogadaEm).HasColumnName("revogada_em");
        builder.Property(s => s.PushToken).HasColumnName("push_token").HasMaxLength(4096);
        builder.Property(s => s.PushPlataforma).HasColumnName("push_plataforma").HasMaxLength(10);
        builder.Property(s => s.PushRegistradoEm).HasColumnName("push_registrado_em");

        // Busca quente: validar o jti (PK) já é por chave. Este índice acelera
        // "achar a sessão ativa do acesso" na hora de revogar no novo login.
        builder.HasIndex(s => new { s.CidadaoAcessoId, s.RevogadaEm })
            .HasDatabaseName("ix_cidadao_sessao_acesso_ativa");

        // Registrar o aparelho tira o mesmo token de qualquer outra sessão (o aparelho trocou de
        // login e não pode receber em dobro). Não é único: entre o "põe aqui" e o "tira de lá"
        // não há transação, e um único violado derrubaria o registro numa corrida inofensiva.
        // Hash, não B-tree: a busca é só por igualdade, e a linha de um B-tree estoura em ~2,7 KB —
        // um token entre isso e os 4096 aceitos derrubaria o PUT com erro 500.
        builder.HasIndex(s => s.PushToken)
            .HasDatabaseName("ix_cidadao_sessao_push_token")
            .HasMethod("hash");
    }
}
