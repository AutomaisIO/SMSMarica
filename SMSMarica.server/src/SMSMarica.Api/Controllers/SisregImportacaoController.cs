using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Importação de agendamentos do SISREG (Arquivo Agendamento TXT — expo_solicitacoes) para
/// SolicitacaoExame. Fatia 1: PREVIEW (só leitura) — o operador envia o TXT e vê o "diff"
/// (novo vs já existe) antes de importar. O EXECUTAR virá na fatia 2.
/// </summary>
[ApiController]
[Route("sisreg/importacao")]
public sealed class SisregImportacaoController(IImportacaoSisregService importacao) : ControllerBase
{
    /// <summary>Preview a partir do upload do TXT. Não escreve nada.</summary>
    [HttpPost("preview")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImportacaoPreviewResultado>(StatusCodes.Status200OK)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportacaoPreviewResultado> Preview(
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie o arquivo TXT do SISREG.");

        // O SISREG exporta em ISO-8859-1 (latin-1).
        using var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.Latin1);
        var conteudo = await reader.ReadToEndAsync(cancellationToken);
        return await importacao.PreviewDeTextoAsync(conteudo, cancellationToken);
    }
}
