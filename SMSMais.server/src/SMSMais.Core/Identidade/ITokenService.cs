using SMSMais.Data.Entities;

namespace SMSMais.Core.Identidade;

/// <summary>Abstração para emissão de JWT — implementação fica no projeto Api.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiraEm) GerarToken(Usuario usuario);
}
