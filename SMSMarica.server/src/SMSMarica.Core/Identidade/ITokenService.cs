using SMSMais.Data.Entities;

namespace SMSMarica.Core.Identidade;

/// <summary>Abstração para emissão de JWT — implementação fica no projeto Api.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiraEm) GerarToken(Usuario usuario);
}
