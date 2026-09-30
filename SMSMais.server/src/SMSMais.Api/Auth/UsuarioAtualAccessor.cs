using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SMSMais.Core.Identidade;

namespace SMSMais.Api.Auth;

internal sealed class UsuarioAtualAccessor(IHttpContextAccessor http) : IUsuarioAtualAccessor
{
    private readonly IHttpContextAccessor _http = http;

    public Guid? UsuarioId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? UnidadeAtivaId
    {
        get
        {
            var valor = _http.HttpContext?.Request.Headers["X-Unidade-Id"].FirstOrDefault();
            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    public string? SessaoId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti);
            return string.IsNullOrWhiteSpace(jti) ? null : jti;
        }
    }

    public DateTime? SessaoExpiraEm
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var exp = user.FindFirstValue(JwtRegisteredClaimNames.Exp);
            return long.TryParse(exp, out var segundos)
                ? DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime
                : null;
        }
    }

    public string? Ip
    {
        get
        {
            var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(ip) ? null : ip;
        }
    }
}
