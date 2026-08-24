using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Institucional.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Institucional;

public sealed partial class InstituicaoService(
    SmsMaisDbContext db,
    IMemoryCache cache,
    IHtmlSanitizer sanitizer) : IInstituicaoService
{
    private const string ChaveCache = "instituicao:singleton";

    // Curto de propósito: quem edita a identidade quer ver o efeito na hora, e o SalvarAsync
    // só invalida o cache DESTE processo — server e (futuros) workers têm o seu.
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private readonly SmsMaisDbContext _db = db;
    private readonly IMemoryCache _cache = cache;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;

    /// <summary>Hex #RRGGBB. Ver <see cref="NormalizarCor"/> para o porquê da rigidez.</summary>
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex CorHex();

    public async Task<InstituicaoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ChaveCache, out InstituicaoDto? emCache) && emCache is not null)
            return emCache;

        var i = await _db.Instituicoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Instituicao.IdSingleton, cancellationToken);

        var dto = i is null ? IInstituicaoService.ObterPadrao() : ParaDto(i);
        _cache.Set(ChaveCache, dto, Ttl);
        return dto;
    }

    public async Task<InstituicaoDto> SalvarAsync(
        Guid usuarioId,
        SalvarInstituicaoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new ValidacaoException("instituicao.nome_obrigatorio", "Informe o nome da instituição.");
        if (string.IsNullOrWhiteSpace(request.NomeSecretaria))
            throw new ValidacaoException("instituicao.secretaria_obrigatoria", "Informe o nome da secretaria.");
        if (string.IsNullOrWhiteSpace(request.NomeCurto))
            throw new ValidacaoException("instituicao.nome_curto_obrigatorio", "Informe o nome curto.");

        var uf = (request.Uf ?? string.Empty).Trim().ToUpperInvariant();
        if (uf.Length != 2)
            throw new ValidacaoException("instituicao.uf_invalida", "A UF deve ter 2 letras.");

        var ibge = SomenteDigitos(request.CodigoIbge);
        if (ibge is not null && ibge.Length != 7)
            throw new ValidacaoException("instituicao.ibge_invalido", "O código IBGE do município tem 7 dígitos.");

        var cnpj = SomenteDigitos(request.Cnpj);
        if (cnpj is not null && cnpj.Length != 14)
            throw new ValidacaoException("instituicao.cnpj_invalido", "O CNPJ deve ter 14 dígitos.");

        if (request.DddPadrao is { } ddd && (ddd < 11 || ddd > 99))
            throw new ValidacaoException("instituicao.ddd_invalido", "O DDD deve estar entre 11 e 99.");

        await GarantirMidiaExisteAsync(request.LogoMidiaId, "logo", cancellationToken);
        await GarantirMidiaExisteAsync(request.FaviconMidiaId, "favicon", cancellationToken);

        var i = await _db.Instituicoes
            .FirstOrDefaultAsync(x => x.Id == Instituicao.IdSingleton, cancellationToken);

        var novo = i is null;
        i ??= new Instituicao { Id = Instituicao.IdSingleton };

        i.Nome = request.Nome.Trim();
        i.NomeSecretaria = request.NomeSecretaria.Trim();
        i.NomeCurto = request.NomeCurto.Trim();
        i.Sigla = Limpar(request.Sigla);
        i.Cnpj = cnpj;
        i.CodigoIbge = ibge;
        i.Uf = uf;
        i.DddPadrao = request.DddPadrao;
        i.Endereco = request.Endereco?.ParaEntidade();
        i.Telefone = Limpar(request.Telefone);
        i.EmailContato = Limpar(request.EmailContato);
        i.EmailDpo = Limpar(request.EmailDpo);
        i.WhatsAppNumeroPublico = SomenteDigitos(request.WhatsAppNumeroPublico);
        i.LogoMidiaId = request.LogoMidiaId;
        i.FaviconMidiaId = request.FaviconMidiaId;
        i.CorPrimaria = NormalizarCor(request.CorPrimaria, nameof(request.CorPrimaria));
        i.CorSecundaria = NormalizarCor(request.CorSecundaria, nameof(request.CorSecundaria));
        i.CorGradienteInicio = NormalizarCor(request.CorGradienteInicio, nameof(request.CorGradienteInicio));
        i.CorGradienteFim = NormalizarCor(request.CorGradienteFim, nameof(request.CorGradienteFim));
        i.UrlPainel = NormalizarUrl(request.UrlPainel, nameof(request.UrlPainel));
        i.UrlApp = NormalizarUrl(request.UrlApp, nameof(request.UrlApp));
        i.UrlArquivos = NormalizarUrl(request.UrlArquivos, nameof(request.UrlArquivos));

        // A assinatura é HTML renderizado no login — página anônima. Sanitiza sempre.
        i.AssinaturaProdutoHtml = string.IsNullOrWhiteSpace(request.AssinaturaProdutoHtml)
            ? null
            : _sanitizer.Sanitize(request.AssinaturaProdutoHtml);

        i.AtualizadoPorUsuarioId = usuarioId;
        i.AtualizadoEm = DateTime.UtcNow;

        if (novo) _db.Instituicoes.Add(i);
        await _db.SaveChangesAsync(cancellationToken);

        _cache.Remove(ChaveCache);
        return ParaDto(i);
    }

    private async Task GarantirMidiaExisteAsync(Guid? midiaId, string campo, CancellationToken ct)
    {
        if (midiaId is null) return;
        var existe = await _db.Midias.AsNoTracking().AnyAsync(m => m.Id == midiaId.Value, ct);
        if (!existe)
            throw new NaoEncontradoException($"Mídia do {campo}", midiaId.Value);
    }

    /// <summary>
    /// Só aceita <c>#RRGGBB</c>. O front injeta esse valor em CSS custom properties
    /// (<c>--theme-primary</c>) numa página servida sem autenticação — um valor livre aqui
    /// viraria injeção de CSS. Rejeitar é mais barato do que escapar depois.
    /// </summary>
    private static string? NormalizarCor(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        if (!CorHex().IsMatch(v))
            throw new ValidacaoException("instituicao.cor_invalida", $"{campo}: use o formato #RRGGBB.");
        return v.ToUpperInvariant();
    }

    /// <summary>Aceita só http/https absolutos — a URL vira link clicável em página pública.</summary>
    private static string? NormalizarUrl(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim().TrimEnd('/');
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidacaoException("instituicao.url_invalida", $"{campo}: informe uma URL http(s) completa.");
        }
        return v;
    }

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? SomenteDigitos(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var d = new string([.. v.Where(char.IsAsciiDigit)]);
        return d.Length == 0 ? null : d;
    }

    private static InstituicaoDto ParaDto(Instituicao i) => new(
        i.Nome, i.NomeSecretaria, i.NomeCurto, i.Sigla, i.Cnpj, i.CodigoIbge, i.Uf, i.DddPadrao,
        i.Endereco is null ? null : EnderecoDto.ParaDto(i.Endereco),
        i.Telefone, i.EmailContato, i.EmailDpo, i.WhatsAppNumeroPublico,
        i.LogoMidiaId, i.FaviconMidiaId,
        i.CorPrimaria, i.CorSecundaria, i.CorGradienteInicio, i.CorGradienteFim,
        i.UrlPainel, i.UrlApp, i.UrlArquivos,
        i.AssinaturaProdutoHtml, i.AtualizadoEm);
}
