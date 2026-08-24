using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities;

namespace SMSMais.Data.Configurations;

internal sealed class InstituicaoConfiguration : IEntityTypeConfiguration<Instituicao>
{
    public void Configure(EntityTypeBuilder<Instituicao> builder)
    {
        builder.ToTable("instituicao");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(i => i.NomeSecretaria).HasColumnName("nome_secretaria").HasMaxLength(200).IsRequired();
        builder.Property(i => i.NomeCurto).HasColumnName("nome_curto").HasMaxLength(60).IsRequired();
        builder.Property(i => i.Sigla).HasColumnName("sigla").HasMaxLength(20);
        builder.Property(i => i.Cnpj).HasColumnName("cnpj").HasMaxLength(14);
        builder.Property(i => i.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7);
        builder.Property(i => i.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired();
        builder.Property(i => i.DddPadrao).HasColumnName("ddd_padrao");
        builder.Property(i => i.Telefone).HasColumnName("telefone").HasMaxLength(30);
        builder.Property(i => i.EmailContato).HasColumnName("email_contato").HasMaxLength(200);
        builder.Property(i => i.EmailDpo).HasColumnName("email_dpo").HasMaxLength(200);
        builder.Property(i => i.WhatsAppNumeroPublico).HasColumnName("whatsapp_numero_publico").HasMaxLength(20);

        // Marca. Hex #RRGGBB — 7 caracteres.
        builder.Property(i => i.CorPrimaria).HasColumnName("cor_primaria").HasMaxLength(7);
        builder.Property(i => i.CorSecundaria).HasColumnName("cor_secundaria").HasMaxLength(7);
        builder.Property(i => i.CorGradienteInicio).HasColumnName("cor_gradiente_inicio").HasMaxLength(7);
        builder.Property(i => i.CorGradienteFim).HasColumnName("cor_gradiente_fim").HasMaxLength(7);

        builder.Property(i => i.UrlPainel).HasColumnName("url_painel").HasMaxLength(200);
        builder.Property(i => i.UrlApp).HasColumnName("url_app").HasMaxLength(200);
        builder.Property(i => i.UrlArquivos).HasColumnName("url_arquivos").HasMaxLength(200);

        builder.Property(i => i.AssinaturaProdutoHtml).HasColumnName("assinatura_produto_html").HasColumnType("text");

        builder.Property(i => i.LogoMidiaId).HasColumnName("logo_midia_id");
        builder.Property(i => i.FaviconMidiaId).HasColumnName("favicon_midia_id");

        builder.Property(i => i.AtualizadoPorUsuarioId).HasColumnName("atualizado_por_usuario_id");
        builder.Property(i => i.AtualizadoEm).HasColumnName("atualizado_em");

        builder.OwnsOne(i => i.Endereco, e =>
        {
            e.Property(x => x.Cep).HasColumnName("endereco_cep").HasMaxLength(8);
            e.Property(x => x.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            e.Property(x => x.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            e.Property(x => x.Complemento).HasColumnName("endereco_complemento").HasMaxLength(120);
            e.Property(x => x.Bairro).HasColumnName("endereco_bairro").HasMaxLength(120);
            e.Property(x => x.Cidade).HasColumnName("endereco_cidade").HasMaxLength(120);
            e.Property(x => x.Uf).HasColumnName("endereco_uf").HasMaxLength(2);
            e.Property(x => x.PontoReferencia).HasColumnName("endereco_ponto_referencia").HasMaxLength(200);
        });

        // Restrict nas mídias: apagar o logo por engano não pode derrubar a identidade
        // da instância inteira — o vínculo tem que ser desfeito de propósito.
        builder.HasOne(i => i.LogoMidia)
            .WithMany()
            .HasForeignKey(i => i.LogoMidiaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.FaviconMidia)
            .WithMany()
            .HasForeignKey(i => i.FaviconMidiaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.AtualizadoPorUsuario)
            .WithMany()
            .HasForeignKey(i => i.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
