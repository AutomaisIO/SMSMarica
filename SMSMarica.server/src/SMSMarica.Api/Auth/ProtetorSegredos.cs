using Microsoft.AspNetCore.DataProtection;
using SMSMarica.Core.Inteligencia.Seguranca;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Implementação de <see cref="IProtetorSegredos"/> via ASP.NET Data Protection.
/// Cifra/decifra segredos do módulo IA (token do provedor, senha das bases) em repouso
/// usando um <see cref="IDataProtector"/> com purpose dedicado.
/// </summary>
internal sealed class ProtetorSegredos : IProtetorSegredos
{
    private const string Proposito = "SMSMarica.Ia.Segredos";

    private readonly IDataProtector _protector;

    public ProtetorSegredos(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Proposito);
    }

    public string Proteger(string textoPuro) => _protector.Protect(textoPuro);

    public string Revelar(string textoCifrado) => _protector.Unprotect(textoCifrado);
}
