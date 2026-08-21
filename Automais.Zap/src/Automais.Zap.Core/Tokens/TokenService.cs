using System.Security.Cryptography;
using System.Text;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Tokens;

/// <summary>Token recém-criado. O valor em claro só existe aqui, uma vez.</summary>
public sealed record TokenCriado(TenantToken Registro, string ValorEmClaro);

/// <summary>Quem chamou a API, já resolvido e autorizado.</summary>
public sealed record ChamadorAutenticado(Guid TokenId, Guid TenantId, string TenantNome, bool TodosNumeros);

public interface ITokenService
{
    Task<TokenCriado> CriarAsync(Guid tenantId, string nome, bool todosNumeros, IReadOnlyCollection<Guid> numeroIds, CancellationToken ct = default);
    Task<IReadOnlyList<TenantToken>> ListarAsync(Guid tenantId, CancellationToken ct = default);
    Task RevogarAsync(Guid tokenId, CancellationToken ct = default);

    /// <summary>Resolve o header <c>Authorization: Bearer</c>. Null = não autenticado.</summary>
    Task<ChamadorAutenticado?> AutenticarAsync(string? cabecalho, CancellationToken ct = default);

    /// <summary>O token pode enviar por este número? Devolve o número quando pode.</summary>
    Task<Numero?> AutorizarNumeroAsync(ChamadorAutenticado chamador, string phoneNumberId, CancellationToken ct = default);
}

public sealed class TokenService(ZapDbContext db, TimeProvider relogio, ILogger<TokenService> logger) : ITokenService
{
    private const string Marca = "zap";

    public async Task<TokenCriado> CriarAsync(
        Guid tenantId, string nome, bool todosNumeros, IReadOnlyCollection<Guid> numeroIds, CancellationToken ct = default)
    {
        var prefixo = Hex(6);
        var segredo = Hex(24);
        var emClaro = $"{Marca}_{prefixo}_{segredo}";

        var registro = new TenantToken
        {
            TenantId = tenantId,
            Nome = nome.Trim(),
            Prefixo = prefixo,
            Hash = Resumo(emClaro),
            TodosNumeros = todosNumeros,
            CriadoEm = relogio.GetUtcNow(),
        };
        db.TenantTokens.Add(registro);

        if (!todosNumeros)
        {
            foreach (var id in numeroIds.Distinct())
            {
                db.TenantTokenNumeros.Add(new TenantTokenNumero { Token = registro, NumeroId = id });
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Token {Prefixo} criado para o tenant {Tenant}.", prefixo, tenantId);

        return new TokenCriado(registro, emClaro);
    }

    public async Task<IReadOnlyList<TenantToken>> ListarAsync(Guid tenantId, CancellationToken ct = default)
        => await db.TenantTokens.AsNoTracking()
            .Include(t => t.Numeros).ThenInclude(n => n.Numero)
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CriadoEm)
            .ToListAsync(ct);

    public async Task RevogarAsync(Guid tokenId, CancellationToken ct = default)
    {
        var token = await db.TenantTokens.FirstOrDefaultAsync(t => t.Id == tokenId, ct);
        if (token is null || token.RevogadoEm is not null) return;

        // Revogar marca, não apaga: saber que existiu um token e quando ele parou de valer é
        // o que permite explicar um incidente depois.
        token.RevogadoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Token {Prefixo} revogado.", token.Prefixo);
    }

    public async Task<ChamadorAutenticado?> AutenticarAsync(string? cabecalho, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cabecalho)) return null;

        var valor = cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? cabecalho[7..].Trim()
            : cabecalho.Trim();

        var partes = valor.Split('_');
        if (partes.Length != 3 || partes[0] != Marca) return null;

        var prefixo = partes[1];
        var token = await db.TenantTokens
            .Include(t => t.Tenant)
            .FirstOrDefaultAsync(t => t.Prefixo == prefixo, ct);

        if (token is null)
        {
            // Compara contra um resumo descartável para que token inexistente e token errado
            // levem o mesmo tempo.
            Confere(Resumo("descartavel"), Resumo(valor));
            return null;
        }

        if (!Confere(token.Hash, Resumo(valor))) return null;
        if (token.RevogadoEm is not null) return null;

        // Tenant suspenso derruba o token junto: a alavanca comercial vale para os dois
        // sentidos, não só para o recebimento.
        if (token.Tenant is null || !token.Tenant.Ativo || token.Tenant.SuspensoEm is not null) return null;

        token.UltimoUsoEm = relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return new ChamadorAutenticado(token.Id, token.TenantId, token.Tenant.Nome, token.TodosNumeros);
    }

    public async Task<Numero?> AutorizarNumeroAsync(
        ChamadorAutenticado chamador, string phoneNumberId, CancellationToken ct = default)
    {
        var numero = await db.Numeros.AsNoTracking()
            .Include(n => n.Waba)
            .FirstOrDefaultAsync(n => n.PhoneNumberId == phoneNumberId, ct);

        if (numero is null || !numero.Ativo) return null;

        // O número tem de ser do tenant do token — senão um cliente enviaria pela linha de
        // outro município só sabendo o phone_number_id, que não é segredo.
        if (numero.Waba!.TenantId != chamador.TenantId) return null;

        if (chamador.TodosNumeros) return numero;

        var permitido = await db.TenantTokenNumeros
            .AnyAsync(x => x.TokenId == chamador.TokenId && x.NumeroId == numero.Id, ct);

        return permitido ? numero : null;
    }

    private static string Hex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    private static string Resumo(string valor)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    private static bool Confere(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
