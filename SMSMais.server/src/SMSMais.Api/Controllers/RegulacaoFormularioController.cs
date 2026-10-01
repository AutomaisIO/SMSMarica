using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Apoio ao formulário da solicitação (ADR-0052) — hoje, a caixa de CID da Hipótese do fluxo
/// Externo. A definição do formulário fica em <see cref="RegulacaoSolicitacoesController"/>.
/// </summary>
[ApiController]
[Route("regulacao/solicitacoes/formulario")]
public sealed class RegulacaoFormularioController(IRegulacaoCidService cids) : ControllerBase
{
    /// <summary>
    /// CID que o sistema de destino aceita como Hipótese para aquele procedimento: do espelho
    /// quando o recurso já foi copiado, ao vivo no SER/SERNIT enquanto não. Consulta — no ao vivo
    /// é só o fetch de sugestões, nada é gravado.
    /// </summary>
    [HttpGet("cids")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<CidRegulacaoSugestoesDto>(StatusCodes.Status200OK)]
    public Task<CidRegulacaoSugestoesDto> Cids(
        [FromQuery] Guid procedimentoId,
        [FromQuery] SistemaRegulacao? sistema,
        // `string?` de propósito: vazio é pedido legítimo (lista tudo), e `string` ganharia um
        // Required implícito que o reprova — mesma armadilha do endpoint do SER.
        [FromQuery] string? termo,
        CancellationToken cancellationToken) =>
        cids.BuscarAsync(procedimentoId, sistema, termo ?? string.Empty, cancellationToken);
}
