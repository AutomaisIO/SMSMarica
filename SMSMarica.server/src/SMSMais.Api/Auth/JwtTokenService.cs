using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMSMais.Core.Identidade;
using SMSMais.Data.Entities;

namespace SMSMais.Api.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiraEm) GerarToken(Usuario usuario)
    {
        if (string.IsNullOrWhiteSpace(_opt.Key))
        {
            throw new InvalidOperationException("Auth:Jwt:Key não configurada.");
        }

        var agora = DateTime.UtcNow;
        var expira = agora.AddMinutes(_opt.ExpiraEmMinutos);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, usuario.NomeCompleto),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        // E-mail é opcional (usuário pode logar só com CPF).
        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, usuario.Email));
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

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expira);
    }
}
