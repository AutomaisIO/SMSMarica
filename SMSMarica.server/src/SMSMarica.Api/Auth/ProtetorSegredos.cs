using Microsoft.AspNetCore.DataProtection;
using SMSMais.Core.Inteligencia.Seguranca;

namespace SMSMarica.Api.Auth;

/// <summary>
/// Implementação de <see cref="IProtetorSegredos"/> via ASP.NET Data Protection.
/// Cifra/decifra segredos do módulo IA (token do provedor, senha das bases) em repouso
/// usando um <see cref="IDataProtector"/> com purpose dedicado.
/// </summary>
internal sealed class ProtetorSegredos : IProtetorSegredos
{
    // NÃO RENOMEAR ESTE VALOR. O purpose entra na derivação da chave do Data Protection: trocar
    // um caractere torna indecifrável tudo o que já foi cifrado com ele — credenciais de
    // integração, Spaces, SISREG, WhatsApp, PEP, IA, TFD e Geo. É um literal congelado, não o
    // nome do produto, e sobrevive de propósito ao rename para SMSMais (ADR-0046, carve-out).
    private const string Proposito = "SMSMarica.Ia.Segredos";

    private readonly IDataProtector _protector;

    public ProtetorSegredos(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Proposito);
    }

    public string Proteger(string textoPuro) => _protector.Protect(textoPuro);

    public string Revelar(string textoCifrado) => _protector.Unprotect(textoCifrado);
}
