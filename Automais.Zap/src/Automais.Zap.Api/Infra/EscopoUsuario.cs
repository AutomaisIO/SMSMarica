using System.Security.Claims;
using Automais.Zap.Core.Admin;
using Automais.Zap.Data.Entities;

namespace Automais.Zap.Api.Infra;

/// <summary>
/// Quem está logado e o que ele pode enxergar.
///
/// Todo acesso a dado de tenant passa por <see cref="PodeVerAsync"/>. A regra é fail-closed:
/// na dúvida, não vê. Um furo aqui não é bug de tela — é dado de um município aparecendo para
/// outro, que é exatamente o que o produto promete não fazer.
/// </summary>
public sealed class EscopoUsuario(IHttpContextAccessor acessor, IAdminService admin)
{
    public const string ClaimGlobal = "zap:global";

    /// <summary>Politica aplicada a pasta /Pages/Admin/Plataforma.</summary>
    public const string PoliticaGlobal = "Global";

    public const string CookieTenant = "zap.tenant";

    private IReadOnlyList<Tenant>? _visiveis;

    /// <summary>
    /// Seleção feita NESTA requisição. O cookie só chega na próxima; sem isto, a primeira
    /// resposta depois de trocar de tenant por URL renderia o shell do tenant anterior.
    /// </summary>
    private Guid? _selecionadoAgora;

    private ClaimsPrincipal? Usuario => acessor.HttpContext?.User;

    public bool Autenticado => Usuario?.Identity?.IsAuthenticated == true;

    public bool Global => Usuario?.FindFirst(ClaimGlobal)?.Value == "1";

    public Guid UsuarioId =>
        Guid.TryParse(Usuario?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    public string Nome => Usuario?.FindFirst(ClaimTypes.Name)?.Value ?? "";

    public async Task<IReadOnlyList<Tenant>> VisiveisAsync(CancellationToken ct = default)
        => _visiveis ??= await admin.TenantsVisiveisAsync(UsuarioId, Global, ct);

    public async Task<bool> PodeVerAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!Autenticado) return false;
        if (Global) return true;
        return (await VisiveisAsync(ct)).Any(t => t.Id == tenantId);
    }

    /// <summary>
    /// Tenant selecionado no seletor do cabeçalho. Só devolve algo que o usuário pode ver —
    /// um cookie adulterado não vira acesso.
    /// </summary>
    public async Task<Tenant?> SelecionadoAsync(CancellationToken ct = default)
    {
        var visiveis = await VisiveisAsync(ct);
        if (visiveis.Count == 0) return null;

        if (_selecionadoAgora is { } agora)
        {
            var recem = visiveis.FirstOrDefault(t => t.Id == agora);
            if (recem is not null) return recem;
        }

        var bruto = acessor.HttpContext?.Request.Cookies[CookieTenant];
        if (Guid.TryParse(bruto, out var id))
        {
            var achado = visiveis.FirstOrDefault(t => t.Id == id);
            if (achado is not null) return achado;
        }

        // Sem seleção válida: quem só tem um tenant não deveria precisar escolher.
        return visiveis.Count == 1 ? visiveis[0] : null;
    }

    public void Selecionar(Guid tenantId)
    {
        _selecionadoAgora = tenantId;
        acessor.HttpContext?.Response.Cookies.Append(CookieTenant, tenantId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(30),
        });
    }
}
