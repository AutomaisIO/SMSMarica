using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.PesquisasSatisfacao;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Destino do botão do WhatsApp: conta o clique e encaminha para a pesquisa da AvanteSocial.
///
/// <para><b>Mora na API, e não no domínio do app, de propósito.</b> O PWA do cidadão declara
/// escopo <c>/</c> e o service worker tem <c>navigateFallback</c> para o index — com a URL em
/// <c>app.smsmarica.online</c>, quem tem o app instalado receberia a tela do aplicativo servida
/// do cache, sem a requisição sequer chegar ao servidor. Aqui não há PWA nem service worker:
/// abre no navegador, igual nos dois sistemas.</para>
///
/// <para>O encaminhamento sai <b>sem identificador</b>. A AvanteSocial recebe a resposta sem
/// saber de quem; nós guardamos o clique sem saber o que foi respondido. É a separação que
/// sustenta a promessa de anonimato feita ao paciente na mensagem.</para>
/// </summary>
[ApiController]
[Route("pesquisa")]
[AllowAnonymous]
public sealed class PesquisaRedirectController(IPesquisasSatisfacaoService pesquisas) : ControllerBase
{
    /// <summary>Conta o clique e redireciona (302) para o link da unidade.</summary>
    [HttpGet("{token:guid}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Abrir(Guid token, CancellationToken cancellationToken)
    {
        var destino = await pesquisas.RegistrarCliqueAsync(token, cancellationToken);

        // Sem cache: o contador tem de ver cada abertura, e um 302 cacheado esconderia as
        // repetidas — que são justamente o sinal de quem voltou ao link.
        Response.Headers.CacheControl = "no-store";
        return Redirect(destino);
    }
}
