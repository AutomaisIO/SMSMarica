using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Institucional;
using SMSMarica.Core.Institucional.Dtos;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Institucional;

/// <summary>
/// Identidade da instituição desta instância (ADR-0043).
///
/// <para>
/// O que estes testes travam não é CRUD: é o fato de que esta linha é a <b>única</b> coisa que
/// distingue a instalação de um município da de outro. Se ela aceitar lixo, o lixo sai no PDF de
/// laudo, na página pública de verificação e na tela de login — inclusive para quem não está
/// autenticado.
/// </para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class InstituicaoServiceTests(PostgresFixture fixture)
{
    private readonly PostgresFixture _fixture = fixture;

    private InstituicaoService CriarServico(out SmsMaricaDbContextWrapper wrapper)
    {
        var db = _fixture.CriarDbContext();
        wrapper = new SmsMaricaDbContextWrapper(db);
        return new InstituicaoService(db, new MemoryCache(new MemoryCacheOptions()), new HtmlSanitizer());
    }

    /// <summary>
    /// Cria um usuário real para carimbar <c>atualizado_por</c>. A FK é de propósito: saber quem
    /// trocou a identidade da instituição é auditoria, não enfeite.
    /// </summary>
    private async Task<Guid> SemearUsuarioAsync()
    {
        await using var db = _fixture.CriarDbContext();
        var u = new SMSMarica.Data.Entities.Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "Teste Instituição",
            Email = $"inst-{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static SalvarInstituicaoRequest Valida(
        string? corPrimaria = "#C8102E",
        string? urlPainel = "https://exemplo.gov.br",
        string? assinatura = null,
        string uf = "RJ",
        int? ddd = 21) =>
        new(
            Nome: "Prefeitura Municipal de Exemplo",
            NomeSecretaria: "Secretaria Municipal de Saúde de Exemplo",
            NomeCurto: "Saúde Exemplo",
            Sigla: "SMS",
            Cnpj: "12345678000199",
            CodigoIbge: "3302700",
            Uf: uf,
            DddPadrao: ddd,
            Endereco: null,
            Telefone: "2137315313",
            EmailContato: "contato@exemplo.gov.br",
            EmailDpo: "lgpd@exemplo.gov.br",
            WhatsAppNumeroPublico: "(21) 3731-5313",
            LogoMidiaId: null,
            FaviconMidiaId: null,
            CorPrimaria: corPrimaria,
            CorSecundaria: null,
            CorGradienteInicio: null,
            CorGradienteFim: null,
            UrlPainel: urlPainel,
            UrlApp: null,
            UrlArquivos: null,
            AssinaturaProdutoHtml: assinatura);

    [Fact]
    public async Task Instancia_nao_configurada_devolve_padrao_neutro_sem_nome_de_municipio()
    {
        // Numa instância recém-provisionada a tabela está vazia. O sistema tem que subir — mas
        // NÃO pode se apresentar como a prefeitura de outro município enquanto isso.
        var padrao = IInstituicaoService.ObterPadrao();

        Assert.DoesNotContain("Maricá", padrao.NomeSecretaria);
        Assert.DoesNotContain("Maricá", padrao.Nome);
        Assert.DoesNotContain("Maricá", padrao.NomeCurto);
        Assert.Null(padrao.WhatsAppNumeroPublico);
        Assert.Null(padrao.EmailDpo);
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("vermelho")]
    [InlineData("#C8102")]           // 5 dígitos
    [InlineData("#C8102EE")]         // 7 dígitos
    [InlineData("red; } body { display:none")]   // injeção de CSS
    [InlineData("#000;}*{background:url(http://evil/x)")]
    public async Task Cor_fora_do_formato_hex_e_recusada(string cor)
    {
        // A cor é interpolada DENTRO de <style> numa página anônima; escapar HTML não protege
        // CSS. Recusar na entrada é a única defesa que não depende de quem renderiza.
        var svc = CriarServico(out var w);
        await using var _ = w;

        await Assert.ThrowsAsync<ValidacaoException>(
            () => svc.SalvarAsync(Guid.NewGuid(), Valida(corPrimaria: cor)));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("exemplo.gov.br")]   // sem esquema
    [InlineData("file:///etc/passwd")]
    public async Task Url_que_nao_e_http_e_recusada(string url)
    {
        var svc = CriarServico(out var w);
        await using var _ = w;

        await Assert.ThrowsAsync<ValidacaoException>(
            () => svc.SalvarAsync(Guid.NewGuid(), Valida(urlPainel: url)));
    }

    [Theory]
    [InlineData("R")]
    [InlineData("RIO")]
    [InlineData("")]
    public async Task Uf_precisa_ter_duas_letras(string uf)
    {
        var svc = CriarServico(out var w);
        await using var _ = w;

        await Assert.ThrowsAsync<ValidacaoException>(
            () => svc.SalvarAsync(Guid.NewGuid(), Valida(uf: uf)));
    }

    [Fact]
    public async Task Assinatura_do_produto_e_sanitizada()
    {
        // A assinatura é HTML renderizado na tela de LOGIN — página anônima. Script aqui seria
        // XSS servido a todo mundo antes de qualquer autenticação.
        var svc = CriarServico(out var w);
        await using var _ = w;

        var usuarioId = await SemearUsuarioAsync();
        var salvo = await svc.SalvarAsync(
            usuarioId,
            Valida(assinatura: "<a href=\"https://automais.io\">Automais</a><script>alert(1)</script>"));

        Assert.NotNull(salvo.AssinaturaProdutoHtml);
        Assert.DoesNotContain("<script", salvo.AssinaturaProdutoHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Automais", salvo.AssinaturaProdutoHtml);
    }

    [Fact]
    public async Task Salvar_duas_vezes_atualiza_a_mesma_linha_singleton()
    {
        // Se cada salvamento criasse linha nova, a instância passaria a ter duas identidades e
        // qual delas o front pega viraria sorteio.
        var svc = CriarServico(out var w);
        await using var _ = w;

        var usuarioId = await SemearUsuarioAsync();
        await svc.SalvarAsync(usuarioId, Valida());
        await svc.SalvarAsync(usuarioId, Valida() with { NomeCurto = "Outro Nome" });

        await using var db = _fixture.CriarDbContext();
        var linhas = await db.Instituicoes.CountAsync();
        Assert.Equal(1, linhas);

        var atual = await svc.ObterAsync();
        Assert.Equal("Outro Nome", atual.NomeCurto);
    }

    [Fact]
    public async Task Whatsapp_publico_e_normalizado_para_so_digitos()
    {
        // O número é consumido por link wa.me no app do cidadão — máscara quebra o link.
        var svc = CriarServico(out var w);
        await using var _ = w;

        var usuarioId = await SemearUsuarioAsync();
        var salvo = await svc.SalvarAsync(usuarioId, Valida());

        Assert.Equal("2137315313", salvo.WhatsAppNumeroPublico);
    }

    /// <summary>Descarta o DbContext criado junto com o service.</summary>
    public sealed class SmsMaricaDbContextWrapper(SMSMarica.Data.SmsMaricaDbContext db) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
