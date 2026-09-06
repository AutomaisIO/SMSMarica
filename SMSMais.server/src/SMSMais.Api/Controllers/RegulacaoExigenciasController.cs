using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Caixinhas de exigência documental de uma solicitação e seus anexos (ADR-0052).
///
/// <para>O conteúdo vive no Spaces; o banco guarda a chave. Nenhum endpoint aqui escreve em
/// sistema de regulação — subir anexo para o SER/SERNIT é do incremento 5, atrás do gate de
/// escrita externa (D-11).</para>
/// </summary>
[ApiController]
[Route("regulacao/solicitacoes/{solicitacaoId:guid}/exigencias")]
public sealed class RegulacaoExigenciasController(IRegulacaoExigenciaService servico) : ControllerBase
{
    /// <summary>Limite de corpo por requisição. Fica acima do teto configurável de 15 MB para o
    /// arquivo, porque o multipart carrega cabeçalhos e o erro amigável tem de vir do service —
    /// cortar no pipeline devolveria 413 seco, sem dizer qual é o limite.</summary>
    private const int LimiteCorpoBytes = 60 * 1024 * 1024;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ExigenciaDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ExigenciaDto>> Listar(Guid solicitacaoId, CancellationToken cancellationToken) =>
        servico.ListarAsync(solicitacaoId, cancellationToken);

    /// <summary>Cria (ou devolve) a caixinha "Anexos gerais", que toda solicitação tem.</summary>
    [HttpPost("anexos-gerais")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ExigenciaDto>(StatusCodes.Status200OK)]
    public Task<ExigenciaDto> GarantirAnexosGerais(Guid solicitacaoId, CancellationToken cancellationToken) =>
        servico.GarantirAnexosGeraisAsync(solicitacaoId, cancellationToken);

    [HttpPost("{exigenciaId:guid}/arquivos")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [RequestSizeLimit(LimiteCorpoBytes)]
    [ProducesResponseType<ArquivoExigenciaDto>(StatusCodes.Status200OK)]
    public async Task<ArquivoExigenciaDto> Anexar(
        Guid solicitacaoId, Guid exigenciaId, IFormFile arquivo, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);

        return await servico.AnexarAsync(
            solicitacaoId, exigenciaId, arquivo.FileName, arquivo.ContentType, ms.ToArray(),
            cancellationToken);
    }

    [HttpDelete("arquivos/{arquivoId:guid}")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(
        Guid solicitacaoId, Guid arquivoId, CancellationToken cancellationToken)
    {
        await servico.RemoverArquivoAsync(solicitacaoId, arquivoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Conteúdo do anexo, para pré-visualizar na tela.</summary>
    [HttpGet("arquivos/{arquivoId:guid}/conteudo")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Conteudo(
        Guid solicitacaoId, Guid arquivoId, CancellationToken cancellationToken)
    {
        var a = await servico.ObterConteudoAsync(solicitacaoId, arquivoId, cancellationToken);
        // `inline`: a tela mostra a foto/PDF sem forçar download.
        return File(a.Conteudo, a.ContentType, a.Nome, enableRangeProcessing: false);
    }
}
