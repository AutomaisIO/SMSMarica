using System.Text;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Importação de agendamentos do SISREG (export de agendamentos — expo_solicitacoes) para
/// SolicitacaoExame. Aceita TXT (cabeçalho de unidade) ou CSV (cabeçalho de colunas; a unidade
/// executante vem do nome do arquivo). PREVIEW (só leitura) mostra o "diff" (novo vs já existe);
/// EXECUTAR importa uma marcação por vez.
/// </summary>
[ApiController]
[Route("sisreg/importacao")]
public sealed class SisregImportacaoController(IImportacaoSisregService importacao) : ControllerBase
{
    /// <summary>Preview a partir do upload do arquivo (TXT ou CSV). Não escreve nada.</summary>
    [HttpPost("preview")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImportacaoPreviewResultado>(StatusCodes.Status200OK)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportacaoPreviewResultado> Preview(
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie o arquivo (TXT ou CSV) do SISREG.");

        // O SISREG exporta em ISO-8859-1 (latin-1); o CSV sai em ASCII (subconjunto), então
        // ler como latin-1 serve para os dois.
        using var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.Latin1);
        var conteudo = await reader.ReadToEndAsync(cancellationToken);
        return await importacao.PreviewDeTextoAsync(conteudo, arquivo.FileName, cancellationToken);
    }

    /// <summary>Importa UMA marcação (por código) do arquivo enviado — roda o fluxo inteiro. ESCRITA.</summary>
    [HttpPost("executar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoExecucaoResultado>(StatusCodes.Status200OK)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportacaoExecucaoResultado> Executar(
        IFormFile arquivo,
        [FromForm] string codigo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            throw new ValidacaoException("importacao.arquivo_ausente", "Envie o arquivo (TXT ou CSV) do SISREG.");
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ValidacaoException("importacao.codigo_ausente", "Informe o código da marcação a importar.");

        using var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.Latin1);
        var conteudo = await reader.ReadToEndAsync(cancellationToken);
        return await importacao.ExecutarUmAsync(conteudo, codigo, arquivo.FileName, cancellationToken);
    }
}
