using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMarica.Core.ApiTokens;
using SMSMais.Data;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Autenticação por chave de serviço enviada no header <c>X-API-Key</c>.
/// Usada por integrações externas (ex.: CentralIA) que não têm JWT de usuário.
/// O principal emitido é de SERVIÇO (claim <see cref="ClaimTokenType"/> =
/// <see cref="ValorServico"/>), reconhecido pelo <see cref="RequerPermissaoAttribute"/>
/// como acesso pleno à API.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SmsMaisDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "ApiKey";
    public const string Header = "X-API-Key";
    public const string ClaimTokenType = "token_type";
    public const string ClaimApiTokenId = "api_token_id";
    public const string ValorServico = "service";

    private readonly SmsMaisDbContext _db = db;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sem o header: não é "falha", é só "esse esquema não se aplica".
        // Deixa o pipeline tentar o JWT (multi-scheme).
        if (!Request.Headers.TryGetValue(Header, out var valores))
        {
            return AuthenticateResult.NoResult();
        }

        var chave = valores.ToString();
        if (string.IsNullOrWhiteSpace(chave))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = ApiTokenHasher.Hash(chave);
        var token = await _db.ApiTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Ativo, Context.RequestAborted);

        if (token is null)
        {
            return AuthenticateResult.Fail("Token de API inválido ou revogado.");
        }

        // Best-effort: registra o último uso sem derrubar a autenticação se falhar.
        try
        {
            token.UltimoUsoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync(Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Falha ao atualizar ultimo_uso_em do token {Id}.", token.Id);
        }

        var claims = new[]
        {
            new Claim(ClaimTokenType, ValorServico),
            new Claim(ClaimApiTokenId, token.Id.ToString()),
            new Claim(ClaimTypes.Name, token.Nome),
        };
        var identity = new ClaimsIdentity(claims, Esquema);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Esquema);
        return AuthenticateResult.Success(ticket);
    }
}
