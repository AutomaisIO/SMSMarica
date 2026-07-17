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

    /// <summary>Linhas do SISREG que não viraram solicitação (com o RAW). Pendentes por padrão.</summary>
    [HttpGet("falhas")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ImportacaoFalhaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ImportacaoFalhaDto>> ListarFalhas(
        [FromQuery] bool somentePendentes = true,
        CancellationToken cancellationToken = default)
        => await importacao.ListarFalhasAsync(somentePendentes, cancellationToken);

    /// <summary>"Validar": reimporta a linha a partir do RAW guardado — não precisa do arquivo de
    /// novo. Se a solicitação já existir, resolve a falha em vez de duplicar. ESCRITA.</summary>
    [HttpPost("falhas/{id:guid}/reprocessar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ImportacaoFalhaReprocessoResultado>(StatusCodes.Status200OK)]
    public async Task<ImportacaoFalhaReprocessoResultado> ReprocessarFalha(
        Guid id,
        CancellationToken cancellationToken)
        => await importacao.ReprocessarFalhaAsync(id, cancellationToken);

    /// <summary>Tira a linha da lista sem importar (inválida na origem, registro cancelado…).
    /// Não apaga nada: só marca a falha como resolvida. Mesma permissão do importar — quem toca
    /// a fila de importação é quem tria os erros dela.</summary>
    [HttpPost("falhas/{id:guid}/descartar")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Inclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DescartarFalha(
        Guid id,
        [FromBody] DescartarFalhaRequest? corpo,
        CancellationToken cancellationToken)
    {
        await importacao.DescartarFalhaAsync(id, corpo?.Nota, cancellationToken);
        return NoContent();
    }
}

/// <summary>Motivo do descarte (opcional) — fica na trilha da falha.</summary>
public sealed record DescartarFalhaRequest(string? Nota);
