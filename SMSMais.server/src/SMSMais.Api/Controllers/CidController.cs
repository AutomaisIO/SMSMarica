using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Cid;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Catálogo CID-10 canônico da instância (<c>smsmarica.cid</c>). Hoje expõe só a sincronização
/// administrativa — a leitura das descrições é interna (<see cref="ICidCatalogoService"/>), usada
/// para mostrar o "diagnóstico inicial" do pedido na ficha de solicitação e na anamnese (ticket #155).
/// </summary>
[ApiController]
[Route("cid")]
public sealed class CidController(ICidCatalogoSyncService sync) : ControllerBase
{
    /// <summary>
    /// Reconsolida a nossa tabela de CID a partir dos espelhos do SER e do SERNIT. Idempotente e
    /// aditivo (upsert por código; não apaga). Também roda sozinho ao fim da importação de cada
    /// catálogo SER/SERNIT — este endpoint é o disparo manual.
    /// </summary>
    [HttpPost("sincronizar")]
    [RequerPermissao(ModuloPermissao.RegulacaoConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<CidCatalogoSyncResultadoDto>> Sincronizar(CancellationToken ct)
        => Ok(await sync.SincronizarAsync(ct));
}
