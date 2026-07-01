using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities;

namespace SMSMarica.Data.Configurations;

internal sealed class RegistroErroConfiguration : IEntityTypeConfiguration<RegistroErro>
{
    public void Configure(EntityTypeBuilder<RegistroErro> builder)
    {
        builder.ToTable("registro_erro");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CodigoReferencia).HasColumnName("codigo_referencia").HasMaxLength(40).IsRequired();
        builder.Property(e => e.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(e => e.Metodo).HasColumnName("metodo").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Caminho).HasColumnName("caminho").HasMaxLength(400).IsRequired();
        builder.Property(e => e.QueryString).HasColumnName("query_string").HasMaxLength(2000);
        builder.Property(e => e.StatusCode).HasColumnName("status_code").IsRequired();
        builder.Property(e => e.TipoExcecao).HasColumnName("tipo_excecao").HasMaxLength(300).IsRequired();
        builder.Property(e => e.Mensagem).HasColumnName("mensagem").HasColumnType("text").IsRequired();
        builder.Property(e => e.StackTrace).HasColumnName("stack_trace").HasColumnType("text");
        builder.Property(e => e.Interna).HasColumnName("interna").HasColumnType("text");
        builder.Property(e => e.TraceId).HasColumnName("trace_id").HasMaxLength(120);
        builder.Property(e => e.UsuarioId).HasColumnName("usuario_id");
        builder.Property(e => e.UsuarioNome).HasColumnName("usuario_nome").HasMaxLength(200);
        builder.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);

        builder.HasIndex(e => e.CodigoReferencia).HasDatabaseName("ix_registro_erro_codigo_referencia");
        builder.HasIndex(e => e.CriadoEm).HasDatabaseName("ix_registro_erro_criado_em");
    }
}
