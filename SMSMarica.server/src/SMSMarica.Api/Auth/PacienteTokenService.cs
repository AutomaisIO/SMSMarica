using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Identidade;

namespace SMSMarica.Api.Auth;

public sealed class PacienteTokenService(IOptions<JwtOptions> options) : IPacienteTokenService
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiraEm) Gerar(Guid pacienteId, string nome, string? cpf)
    {
        if (string.IsNullOrWhiteSpace(_opt.Key))
        {
            throw new InvalidOperationException("Auth:Jwt:Key não configurada.");
        }

        var agora = DateTime.UtcNow;
        var expira = agora.AddMinutes(_opt.ExpiraEmMinutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, pacienteId.ToString()),
            new(JwtRegisteredClaimNames.Name, nome),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
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
            expires: expira,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
