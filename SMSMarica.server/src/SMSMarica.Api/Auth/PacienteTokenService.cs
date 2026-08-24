using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Identidade;

namespace SMSMarica.Api.Auth;

public sealed class PacienteTokenService(IOptions<JwtOptions> options) : IPacienteTokenService
{
    private readonly JwtOptions _opt = options.Value;

    public string Gerar(Guid pacienteId, string nome, string? cpf, Guid sessaoJti, DateTime expiraEm)
    {
        if (string.IsNullOrWhiteSpace(_opt.Key))
        {
            throw new InvalidOperationException("Auth:Jwt:Key não configurada.");
        }

        var agora = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, pacienteId.ToString()),
            new(JwtRegisteredClaimNames.Name, nome),
            // jti = id da sessão; validado contra cidadao_sessao a cada request (single-device).
            new(JwtRegisteredClaimNames.Jti, sessaoJti.ToString()),
            new("tipo", "cidadao"),
        };
        if (!string.IsNullOrWhiteSpace(cpf))
        {
            claims.Add(new Claim("cpf", cpf));
        }

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
