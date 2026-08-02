using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Core.PainelInicio;
using SMSMarica.Core.PainelInicio.Dtos;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// O painel da tela de início. Ver ADR-0033.
///
/// <b>Sem <c>[RequerPermissao]</c> de propósito</b>: o painel não expõe nada que o usuário já não
/// pudesse ver navegando, e o gate é por RAIA, dentro do serviço (raia sem permissão volta
/// <c>null</c>). Um módulo novo custaria um valor de enum mais cinco pontos de sincronização no
/// front em troca de nada, e ainda criaria o estado "vejo o painel mas não a tela para onde ele
/// aponta". Mesma régua de <c>GET /tickets/resumo</c>.
/// </summary>
[ApiController]
[Route("painel")]
[Authorize]
public sealed class PainelController(IPainelInicioService painel) : ControllerBase
{
    /// <summary>
    /// O que precisa da atenção do operador agora: cancelamentos do paciente, quem ainda não
    /// respondeu, pendências de importação e quanto está confirmado — tudo numa requisição.
    /// </summary>
    /// <param name="lente">
    /// <c>unidade</c> (padrão) ou <c>municipio</c>. A lente de município é a modelagem da Regulação
    /// — que não é executante nem solicitante — e responde 403 para quem não tem visão global.
    /// </param>
    /// <param name="direcao">
    /// <c>tudo</c> (padrão), <c>executante</c> ou <c>solicitante</c>. Ignorado na lente município.
    /// </param>
    [HttpGet("inicio")]
    [ProducesResponseType<PainelInicioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PainelInicioDto> Inicio(
        [FromQuery] LenteEscopoPainel lente = LenteEscopoPainel.Unidade,
        [FromQuery] DirecaoPainel direcao = DirecaoPainel.Tudo,
        CancellationToken cancellationToken = default)
        => await painel.ObterAsync(lente, direcao, cancellationToken);
}
