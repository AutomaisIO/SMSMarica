using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Importação de agendamentos do SISREG (scraping) para SolicitacaoExame.
/// Fatia 1: PREVIEW (só leitura) — monta o "diff" de um período (novo vs já existe) para
/// o operador conferir antes de importar. O EXECUTAR virá na fatia 2.
/// </summary>
[ApiController]
[Route("sisreg/importacao")]
public sealed class SisregImportacaoController(IImportacaoSisregService importacao) : ControllerBase
{
    /// <summary>Preview do período: lista as marcações do SISREG cruzadas com a nossa base. Não escreve nada.</summary>
    [HttpGet("preview")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImportacaoPreviewResultado>(StatusCodes.Status200OK)]
    public async Task<ImportacaoPreviewResultado> Preview(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        CancellationToken cancellationToken) =>
        await importacao.PreviewAsync(inicio, fim, cancellationToken);
}
