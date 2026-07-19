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
    Task<IReadOnlyList<RamalDto>> ListarAsync(int? unidadeId, CancellationToken ct = default);
    Task<RamalDto> ObterAsync(string numero, CancellationToken ct = default);
    Task<RamalComSecretDto> CriarAsync(CriarRamalRequest request, CancellationToken ct = default);
    Task<RamalDto> AtualizarAsync(string numero, AtualizarRamalRequest request, CancellationToken ct = default);
    Task ExcluirAsync(string numero, CancellationToken ct = default);
    Task<RamalComSecretDto> ResetSecretAsync(string numero, CancellationToken ct = default);
    Task<AdocaoResultadoDto> AdotarAsync(AdotarRamaisRequest request, CancellationToken ct = default);
}

public sealed class RamalService(
    PabxDbContext db,
    IDataProtectionProvider dataProtection,
    IGeradorConfigSip geradorConfig,
    IProvisionamentoService provisionamento,
    IValidator<CriarRamalRequest> criarValidator,
    IValidator<AtualizarRamalRequest> atualizarValidator,
    IValidator<AdotarRamaisRequest> adotarValidator,
    IOptions<AsteriskOptions> options,
    TimeProvider timeProvider,
    ILogger<RamalService> logger) : IRamalService
{
    private readonly AsteriskOptions _opcoes = options.Value;

    public async Task<IReadOnlyList<RamalDto>> ListarAsync(int? unidadeId, CancellationToken ct = default)
    {
        var query = db.Ramais.Include(r => r.Unidade).AsNoTracking();
        if (unidadeId is not null)
            query = query.Where(r => r.UnidadeId == unidadeId);

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
        r.CallerId, r.Ativo, r.Origem, r.CriadoEm, r.AtualizadoEm);
}
