using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Integracoes;

namespace SMSMais.Core.Integracoes.Credenciais;

/// <summary>
/// Store cifrado (uma linha por provedor) das credenciais OAuth. Cifra/decifra via
/// <see cref="IProtetorSegredos"/> (mesmo purpose dos demais segredos). Espelha o padrão
/// de <c>SisregConfiguracaoService</c>.
/// </summary>
public sealed class IntegracaoCredencialService(
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IUsuarioAtualAccessor usuarioAtual) : IIntegracaoCredencialService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IProtetorSegredos _protetor = protetor;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<IntegracaoCredencialDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var existentes = await _db.IntegracaoCredenciais.AsNoTracking()
            .ToDictionaryAsync(c => c.Provedor, cancellationToken);

        // Devolve todos os provedores suportados — placeholder "não configurado" p/ os que faltam.
        return [.. ProvedoresIntegracao.Suportados.Select(kv =>
            existentes.TryGetValue(kv.Key, out var c)
                ? ParaDto(c)
                : new IntegracaoCredencialDto(kv.Key, kv.Value, false, false, null, null, false))];
    }

    public async Task<IntegracaoCredencialDto> ObterAsync(string provedor, CancellationToken cancellationToken = default)
    {
        provedor = Normalizar(provedor);
        var c = await _db.IntegracaoCredenciais.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provedor == provedor, cancellationToken);
        return c is not null
            ? ParaDto(c)
            : new IntegracaoCredencialDto(provedor, ProvedoresIntegracao.Rotulo(provedor), false, false, null, null, false);
    }

    public async Task AtualizarAsync(
        string provedor, AtualizarIntegracaoCredencialRequest request, CancellationToken cancellationToken = default)
    {
        provedor = Normalizar(provedor);
        var c = await ObterOuCriarAsync(provedor, cancellationToken);

        c.RedirectUri = string.IsNullOrWhiteSpace(request.RedirectUri) ? null : request.RedirectUri.Trim();
        c.ParametrosJson = string.IsNullOrWhiteSpace(request.ParametrosJson) ? null : request.ParametrosJson.Trim();
        c.Ativo = request.Ativo;

        // Vazio = mantém o atual; preenchido = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            c.ClientIdCifrado = _protetor.Proteger(request.ClientId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            c.ClientSecretCifrado = _protetor.Proteger(request.ClientSecret.Trim());
        }

        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task LimparAsync(string provedor, CancellationToken cancellationToken = default)
    {
        provedor = Normalizar(provedor);
        var c = await _db.IntegracaoCredenciais.FirstOrDefaultAsync(x => x.Provedor == provedor, cancellationToken);
        if (c is null) return;

        c.ClientIdCifrado = null;
        c.ClientSecretCifrado = null;
        c.Ativo = false;
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IntegracaoCredencialContexto> ObterContextoAsync(string provedor, CancellationToken cancellationToken = default)
    {
        provedor = Normalizar(provedor);
        var c = await _db.IntegracaoCredenciais.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provedor == provedor, cancellationToken)
            ?? throw new ValidacaoException("integracao.nao_configurada",
                $"Provedor '{provedor}' ainda não configurado.");

        if (!c.Ativo)
        {
            throw new ValidacaoException("integracao.inativa", $"Provedor '{provedor}' está desativado.");
        }

        var clientId = string.IsNullOrEmpty(c.ClientIdCifrado) ? null : _protetor.Revelar(c.ClientIdCifrado);
        var clientSecret = string.IsNullOrEmpty(c.ClientSecretCifrado) ? null : _protetor.Revelar(c.ClientSecretCifrado);

        return new IntegracaoCredencialContexto(
            c.Provedor, clientId, clientSecret, c.RedirectUri, c.ParametrosJson, c.Ativo);
    }

    private static string Normalizar(string provedor)
    {
        var p = (provedor ?? string.Empty).Trim().ToLowerInvariant();
        if (!ProvedoresIntegracao.EhSuportado(p))
        {
            throw new ValidacaoException("integracao.provedor_invalido",
                $"Provedor '{provedor}' não é suportado. Suportados: {string.Join(", ", ProvedoresIntegracao.Suportados.Keys)}.");
        }
        return p;
    }

    private async Task<IntegracaoCredencial> ObterOuCriarAsync(string provedor, CancellationToken cancellationToken)
    {
        var c = await _db.IntegracaoCredenciais.FirstOrDefaultAsync(x => x.Provedor == provedor, cancellationToken);
        if (c is not null) return c;

        c = new IntegracaoCredencial
        {
            Id = Guid.CreateVersion7(),
            Provedor = provedor,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };
        _db.IntegracaoCredenciais.Add(c);
        return c;
    }

    private static IntegracaoCredencialDto ParaDto(IntegracaoCredencial c) => new(
        c.Provedor,
        ProvedoresIntegracao.Rotulo(c.Provedor),
        !string.IsNullOrEmpty(c.ClientIdCifrado),
        !string.IsNullOrEmpty(c.ClientSecretCifrado),
        c.RedirectUri,
        c.ParametrosJson,
        c.Ativo);
}
