using System.Security.Cryptography;
using Automais.Pabx.Api.Asterisk;
using Automais.Pabx.Api.Data;
using Automais.Pabx.Api.Data.Entities;
using Automais.Pabx.Api.Infra.Excecoes;
using Automais.Pabx.Api.Provisionamento;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Automais.Pabx.Api.Ramais;

public interface IRamalService
{
    Task<IReadOnlyList<RamalDto>> ListarAsync(FiltroRamais filtro, CancellationToken ct = default);
    Task<RamalDto> ObterAsync(string numero, CancellationToken ct = default);
    Task<RamalComSecretDto> CriarAsync(CriarRamalRequest request, CancellationToken ct = default);
    Task<RamalDto> AtualizarAsync(string numero, AtualizarRamalRequest request, CancellationToken ct = default);
    Task ExcluirAsync(string numero, CancellationToken ct = default);
    Task<RamalComSecretDto> ResetSecretAsync(string numero, CancellationToken ct = default);
    Task<AdocaoResultadoDto> AdotarAsync(AdotarRamaisRequest request, CancellationToken ct = default);
    Task<ConfigRamalDto> ObterConfigAsync(string numero, CancellationToken ct = default);
    Task<ConfigRamalDto> AtualizarConfigAsync(string numero, AtualizarConfigRamalRequest request, CancellationToken ct = default);
    Task<RamalDto> DefinirDonoAsync(string numero, DefinirDonoRequest request, CancellationToken ct = default);
    Task<FaixaLivreDto> SugerirLivresAsync(TipoRamal tipo, int quantidade, CancellationToken ct = default);
    Task<CredencialSipDto> ObterCredencialAsync(string numero, CancellationToken ct = default);
}

public sealed record FiltroRamais(int? UnidadeId = null, TipoRamal? Tipo = null, string? DonoSistema = null, string? DonoId = null);

public sealed class RamalService(
    PabxDbContext db,
    IDataProtectionProvider dataProtection,
    IGeradorConfigSip geradorConfig,
    IProvisionamentoService provisionamento,
    IValidator<CriarRamalRequest> criarValidator,
    IValidator<AtualizarRamalRequest> atualizarValidator,
    IValidator<AdotarRamaisRequest> adotarValidator,
    IValidator<AtualizarConfigRamalRequest> configValidator,
    IValidator<DefinirDonoRequest> donoValidator,
    IOptions<AsteriskOptions> options,
    TimeProvider timeProvider,
    ILogger<RamalService> logger) : IRamalService
{
    private readonly AsteriskOptions _opcoes = options.Value;

    public async Task<IReadOnlyList<RamalDto>> ListarAsync(FiltroRamais filtro, CancellationToken ct = default)
    {
        var query = db.Ramais.Include(r => r.Unidade).AsNoTracking();
        if (filtro.UnidadeId is not null)
            query = query.Where(r => r.UnidadeId == filtro.UnidadeId);
        if (filtro.Tipo is not null)
            query = query.Where(r => r.Tipo == filtro.Tipo);
        if (!string.IsNullOrWhiteSpace(filtro.DonoSistema))
            query = query.Where(r => r.DonoSistema == filtro.DonoSistema);
        if (!string.IsNullOrWhiteSpace(filtro.DonoId))
            query = query.Where(r => r.DonoId == filtro.DonoId);

        var ramais = await query.OrderBy(r => r.Numero).ToListAsync(ct);
        return [.. ramais.Select(ParaDto)];
    }

    public async Task<RamalDto> ObterAsync(string numero, CancellationToken ct = default)
    {
        var ramal = await db.Ramais.Include(r => r.Unidade).AsNoTracking()
            .FirstOrDefaultAsync(r => r.Numero == numero, ct)
            ?? throw new NaoEncontradoException("Ramal", numero);
        return ParaDto(ramal);
    }

    public async Task<RamalComSecretDto> CriarAsync(CriarRamalRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(criarValidator, request, ct);

        if (await db.Ramais.AnyAsync(r => r.Numero == request.Numero, ct))
            throw new ConflitoException("ramal_duplicado", $"Ramal {request.Numero} já existe no inventário.");

        var faixa = _opcoes.Faixa(request.Tipo);
        if (!faixa.Contem(int.Parse(request.Numero)))
            throw new ValidacaoException(nameof(request.Numero),
                $"Ramal {request.Tipo} deve estar na faixa {faixa.Inicio}–{faixa.Fim}.");

        // Número que já existe no legado da FalarMais viraria seção duplicada no chan_sip.
        if ((await NumerosLegadoAsync(ct)).Contains(request.Numero))
            throw new ConflitoException("ramal_existe_no_legado",
                $"Ramal {request.Numero} já existe no sip_custom.conf. Use a adoção em vez de criar.");

        var unidade = await db.Unidades.FindAsync([request.UnidadeId], ct)
            ?? throw new NaoEncontradoException("Unidade", request.UnidadeId);

        var mac = NormalizarMac(request.Mac);
        await GarantirMacLivreAsync(mac, ramalId: null, ct);
        if (mac is not null && request.Marca is not null)
            await provisionamento.VerificarDisponibilidadeAsync(request.Marca.Value, mac, ct);

        var secret = GerarSecret();
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var ramal = new Ramal
        {
            Numero = request.Numero,
            SecretCifrado = Protetor().Protect(secret),
            UnidadeId = unidade.Id,
            Descricao = request.Descricao,
            Mac = mac,
            Marca = request.Marca,
            Modelo = request.Modelo,
            CallerId = request.CallerId,
            Tipo = request.Tipo,
            // Navegador negocia ulaw/alaw nativamente; gsm só serve aos aparelhos antigos.
            Codecs = request.Tipo == TipoRamal.Softphone ? "ulaw,alaw" : "alaw,ulaw,gsm",
            DonoSistema = Vazio(request.DonoSistema),
            DonoId = Vazio(request.DonoId),
            Origem = OrigemRamal.Gerenciado,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        db.Ramais.Add(ramal);
        await db.SaveChangesAsync(ct);

        await AplicarEProvisionarAsync(ramal, secret, ct);
        logger.LogInformation("Ramal {Numero} criado (unidade {Unidade})", ramal.Numero, unidade.Nome);

        ramal.Unidade = unidade;
        return new RamalComSecretDto(ParaDto(ramal), secret);
    }

    public async Task<RamalDto> AtualizarAsync(string numero, AtualizarRamalRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(atualizarValidator, request, ct);

        var ramal = await db.Ramais.Include(r => r.Unidade)
            .FirstOrDefaultAsync(r => r.Numero == numero, ct)
            ?? throw new NaoEncontradoException("Ramal", numero);

        var unidade = await db.Unidades.FindAsync([request.UnidadeId], ct)
            ?? throw new NaoEncontradoException("Unidade", request.UnidadeId);

        var macNovo = NormalizarMac(request.Mac);
        if (ramal.Tipo == TipoRamal.Softphone && macNovo is not null)
            throw new ValidacaoException(nameof(request.Mac), "Softphone não tem aparelho: não informe MAC.");
        await GarantirMacLivreAsync(macNovo, ramal.Id, ct);
        if (macNovo is not null && macNovo != ramal.Mac && request.Marca is not null)
            await provisionamento.VerificarDisponibilidadeAsync(request.Marca.Value, macNovo, ct);

        // MAC trocado/removido: o XML do aparelho antigo deixa de valer.
        if (ramal.Mac is not null && ramal.Mac != macNovo)
            await provisionamento.RemoverAsync(ramal, ct);

        ramal.UnidadeId = unidade.Id;
        ramal.Descricao = request.Descricao;
        ramal.Mac = macNovo;
        ramal.Marca = request.Marca;
        ramal.Modelo = request.Modelo;
        ramal.CallerId = request.CallerId;
        ramal.Ativo = request.Ativo;
        ramal.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        await AplicarEProvisionarAsync(ramal, Protetor().Unprotect(ramal.SecretCifrado), ct);

        ramal.Unidade = unidade;
        return ParaDto(ramal);
    }

    public async Task ExcluirAsync(string numero, CancellationToken ct = default)
    {
        var ramal = await db.Ramais.FirstOrDefaultAsync(r => r.Numero == numero, ct)
            ?? throw new NaoEncontradoException("Ramal", numero);

        await provisionamento.RemoverAsync(ramal, ct);
        db.Ramais.Remove(ramal);
        await db.SaveChangesAsync(ct);

        if (ramal.Origem == OrigemRamal.Gerenciado)
            await geradorConfig.AplicarAsync(ct);

        logger.LogInformation("Ramal {Numero} excluído do inventário", numero);
    }

    public async Task<RamalComSecretDto> ResetSecretAsync(string numero, CancellationToken ct = default)
    {
        var ramal = await db.Ramais.Include(r => r.Unidade)
            .FirstOrDefaultAsync(r => r.Numero == numero, ct)
            ?? throw new NaoEncontradoException("Ramal", numero);

        if (ramal.Origem == OrigemRamal.Adotado)
            throw new ConflitoException("ramal_adotado",
                "Ramal adotado: o secret vive no sip_custom.conf legado e não é gerenciado por aqui.");

        var secret = GerarSecret();
        ramal.SecretCifrado = Protetor().Protect(secret);
        ramal.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        await AplicarEProvisionarAsync(ramal, secret, ct);
        return new RamalComSecretDto(ParaDto(ramal), secret);
    }

    public async Task<AdocaoResultadoDto> AdotarAsync(AdotarRamaisRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(adotarValidator, request, ct);

        if (!File.Exists(_opcoes.SipCustomConfPath))
            throw new NaoEncontradoException("Arquivo sip_custom.conf", _opcoes.SipCustomConfPath);

        var unidade = await db.Unidades.FindAsync([request.UnidadeId], ct)
            ?? throw new NaoEncontradoException("Unidade", request.UnidadeId);

        var conteudo = await File.ReadAllTextAsync(_opcoes.SipCustomConfPath, ct);
        var secoes = SipCustomParser.SomenteRamais(SipCustomParser.Parse(conteudo))
            .ToDictionary(s => s.Nome);

        var adotados = new List<string>();
        var jaInventariados = new List<string>();
        var naoEncontrados = new List<string>();
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var numero in request.Numeros.Distinct())
        {
            if (!secoes.TryGetValue(numero, out var secao))
            {
                naoEncontrados.Add(numero);
                continue;
            }

            if (await db.Ramais.AnyAsync(r => r.Numero == numero, ct))
            {
                jaInventariados.Add(numero);
                continue;
            }

            db.Ramais.Add(new Ramal
            {
                Numero = numero,
                SecretCifrado = Protetor().Protect(secao.Campos.GetValueOrDefault("secret", "")),
                UnidadeId = unidade.Id,
                Descricao = "Adotado do sip_custom.conf",
                CallerId = secao.Campos.GetValueOrDefault("callerid"),
                Origem = OrigemRamal.Adotado,
                CriadoEm = agora,
                AtualizadoEm = agora,
            });
            adotados.Add(numero);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Adoção: {Adotados} adotados, {Ja} já inventariados, {Nao} não encontrados",
            adotados.Count, jaInventariados.Count, naoEncontrados.Count);

        return new AdocaoResultadoDto(adotados, jaInventariados, naoEncontrados);
    }

    public async Task<ConfigRamalDto> ObterConfigAsync(string numero, CancellationToken ct = default)
    {
        var ramal = await ObterEntidadeAsync(numero, ct);
        return ParaConfigDto(ramal);
    }

    public async Task<ConfigRamalDto> AtualizarConfigAsync(string numero, AtualizarConfigRamalRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(configValidator, request, ct);
        var ramal = await ObterEntidadeAsync(numero, ct);
        GarantirGerenciado(ramal);

        ramal.Contexto = request.Contexto;
        ramal.Codecs = string.Join(',', request.Codecs.Select(c => c.Trim().ToLowerInvariant()).Distinct());
        ramal.CallLimit = request.CallLimit;
        ramal.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        await geradorConfig.AplicarAsync(ct);
        logger.LogInformation("Configuração do ramal {Numero} alterada (contexto {Contexto}, codecs {Codecs}, call-limit {Limite})",
            numero, ramal.Contexto, ramal.Codecs, ramal.CallLimit);
        return ParaConfigDto(ramal);
    }

    public async Task<RamalDto> DefinirDonoAsync(string numero, DefinirDonoRequest request, CancellationToken ct = default)
    {
        await ValidarAsync(donoValidator, request, ct);
        var ramal = await db.Ramais.Include(r => r.Unidade)
            .FirstOrDefaultAsync(r => r.Numero == numero, ct)
            ?? throw new NaoEncontradoException("Ramal", numero);

        ramal.DonoSistema = Vazio(request.Sistema);
        ramal.DonoId = Vazio(request.Id);
        ramal.AtualizadoEm = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return ParaDto(ramal);
    }

    public async Task<FaixaLivreDto> SugerirLivresAsync(TipoRamal tipo, int quantidade, CancellationToken ct = default)
    {
        var faixa = _opcoes.Faixa(tipo);
        if (!faixa.Restrita)
            return new FaixaLivreDto(tipo, null, null, []);

        var ocupados = (await db.Ramais.Select(r => r.Numero).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        ocupados.UnionWith(await NumerosLegadoAsync(ct));

        var sugestoes = Enumerable.Range(faixa.Inicio, faixa.Fim - faixa.Inicio + 1)
            .Select(n => n.ToString())
            .Where(n => !ocupados.Contains(n))
            .Take(Math.Clamp(quantidade, 1, 50))
            .ToList();

        return new FaixaLivreDto(tipo, faixa.Inicio, faixa.Fim, sugestoes);
    }

    public async Task<CredencialSipDto> ObterCredencialAsync(string numero, CancellationToken ct = default)
    {
        var ramal = await ObterEntidadeAsync(numero, ct);

        if (ramal.Tipo != TipoRamal.Softphone)
            throw new ConflitoException("ramal_nao_softphone",
                $"Ramal {numero} é de aparelho físico: a credencial vai por provisionamento, não por esta rota.");
        if (!ramal.Ativo)
            throw new ConflitoException("ramal_inativo", $"Ramal {numero} está inativo.");

        var web = _opcoes.WebRtc;
        return new CredencialSipDto(
            Ramal: ramal.Numero,
            UsuarioSip: ramal.Numero,
            Senha: Protetor().Unprotect(ramal.SecretCifrado),
            Dominio: web.Dominio,
            Uri: $"sip:{ramal.Numero}@{web.Dominio}",
            WssUrl: web.WssUrl,
            NomeExibicao: ramal.CallerId,
            IceServers: [.. web.IceServers.Select(i => new IceServerDto(i.Urls, i.Username, i.Credential))]);
    }

    private async Task<Ramal> ObterEntidadeAsync(string numero, CancellationToken ct) =>
        await db.Ramais.FirstOrDefaultAsync(r => r.Numero == numero, ct)
        ?? throw new NaoEncontradoException("Ramal", numero);

    private static void GarantirGerenciado(Ramal ramal)
    {
        if (ramal.Origem == OrigemRamal.Adotado)
            throw new ConflitoException("ramal_adotado",
                "Ramal adotado: a configuração vive no sip_custom.conf legado e não é gerenciada por aqui.");
    }

    /// <summary>Números que já existem como seção no sip_custom.conf da FalarMais (só leitura).</summary>
    private async Task<HashSet<string>> NumerosLegadoAsync(CancellationToken ct)
    {
        if (!File.Exists(_opcoes.SipCustomConfPath))
            return [];
        var conteudo = await File.ReadAllTextAsync(_opcoes.SipCustomConfPath, ct);
        return SipCustomParser.SomenteRamais(SipCustomParser.Parse(conteudo))
            .Select(sec => sec.Nome)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? Vazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static ConfigRamalDto ParaConfigDto(Ramal r) =>
        new(r.Contexto, BlocoSipChanSip.ListaCodecs(r.Codecs), r.CallLimit);

    private async Task AplicarEProvisionarAsync(Ramal ramal, string secret, CancellationToken ct)
    {
        if (ramal.Origem == OrigemRamal.Gerenciado)
            await geradorConfig.AplicarAsync(ct);

        if (ramal.Ativo && ramal.Mac is not null && ramal.Marca is not null)
            await provisionamento.GerarAsync(ramal, secret, ct);
    }

    private async Task GarantirMacLivreAsync(string? mac, int? ramalId, CancellationToken ct)
    {
        if (mac is null)
            return;
        var ocupado = await db.Ramais.AnyAsync(r => r.Mac == mac && r.Id != ramalId, ct);
        if (ocupado)
            throw new ConflitoException("mac_duplicado", $"MAC {mac} já está associado a outro ramal.");
    }

    private IDataProtector Protetor() => dataProtection.CreateProtector(GeradorConfigChanSip.ProtetorSecret);

    private static async Task ValidarAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var resultado = await validator.ValidateAsync(request, ct);
        if (!resultado.IsValid)
            throw new ValidacaoException(resultado.ToDictionary());
    }

    private static string? NormalizarMac(string? mac) =>
        string.IsNullOrWhiteSpace(mac) ? null : mac.Replace(":", "").Replace("-", "").Trim().ToLowerInvariant();

    /// <summary>Secret SIP forte: 20 caracteres alfanuméricos (sem símbolos que quebram .conf/XML).</summary>
    private static string GerarSecret()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        return string.Create(20, alfabeto, (span, alfa) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = alfa[RandomNumberGenerator.GetInt32(alfa.Length)];
        });
    }

    private static RamalDto ParaDto(Ramal r) => new(
        r.Numero, r.UnidadeId, r.Unidade?.Nome, r.Descricao, r.Mac, r.Marca, r.Modelo,
        r.CallerId, r.Ativo, r.Origem, r.Tipo, r.Contexto, BlocoSipChanSip.ListaCodecs(r.Codecs), r.CallLimit,
        r.DonoSistema, r.DonoId, r.CriadoEm, r.AtualizadoEm);
}
