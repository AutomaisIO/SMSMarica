using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SMSMarica.Core.Identidade;

namespace SMSMarica.Api.Auth;

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
}
