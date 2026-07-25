using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Inteligencia.Conhecimento.Gestao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Gestão do conhecimento (documentação .md) de cada base do módulo IA: ver/editar os documentos,
/// e extrair o modelo da base (tabelas + relacionamentos) direto do schema. Ver ADR-0023.
/// </summary>
[ApiController]
[Route("ia/conhecimento")]
public sealed class ConhecimentoController(IConhecimentoGestaoService service) : ControllerBase
{
    private readonly IConhecimentoGestaoService _service = service;

    [HttpGet("{fonteId:guid}/documentos")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<DocumentoResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DocumentoResumoDto>> Listar(Guid fonteId, CancellationToken ct) =>
        await _service.ListarDocumentosAsync(fonteId, ct);

    [HttpGet("{fonteId:guid}/documentos/{docId:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<DocumentoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DocumentoDetalheDto> Obter(Guid fonteId, Guid docId, CancellationToken ct) =>
        await _service.ObterDocumentoAsync(fonteId, docId, ct);

    /// <summary>Cria um documento manual de conhecimento (regras, relacionamentos, exemplos de SQL).</summary>
    [HttpPost("{fonteId:guid}/documentos")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<DocumentoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<DocumentoDetalheDto> Criar(
        Guid fonteId, [FromBody] SalvarDocumentoDto dto, CancellationToken ct) =>
        await _service.SalvarDocumentoAsync(fonteId, null, dto, ct);

    [HttpPut("{fonteId:guid}/documentos/{docId:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<DocumentoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<DocumentoDetalheDto> Atualizar(
        Guid fonteId, Guid docId, [FromBody] SalvarDocumentoDto dto, CancellationToken ct) =>
        await _service.SalvarDocumentoAsync(fonteId, docId, dto, ct);

    [HttpDelete("{fonteId:guid}/documentos/{docId:guid}")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid fonteId, Guid docId, CancellationToken ct)
    {
        await _service.RemoverDocumentoAsync(fonteId, docId, ct);
        return NoContent();
    }

    /// <summary>
    /// Levanta a estrutura da base (tabelas, colunas e relacionamentos) direto do schema — via a
    /// conexão configurada, inclusive por agente proxy — e gera um documento por tabela + catálogo.
    /// </summary>
    [HttpPost("{fonteId:guid}/extrair-modelo")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<ExtracaoModeloResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ExtracaoModeloResultado> ExtrairModelo(
        Guid fonteId, [FromBody] ExtrairModeloDto dto, CancellationToken ct) =>
        await _service.ExtrairModeloAsync(fonteId, dto, ct);

    /// <summary>
    /// Gera embeddings para os chunks pendentes desta base (backfill). Roda mesmo com o RAG
    /// desligado — serve pra preparar a busca vetorial antes de ligar a flag. Idempotente.
    /// </summary>
    [HttpPost("{fonteId:guid}/embeddings")]
    [RequerPermissao(ModuloPermissao.InteligenciaConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EmbeddingsBackfillResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<EmbeddingsBackfillResultado> GerarEmbeddings(Guid fonteId, CancellationToken ct) =>
        await _service.GerarEmbeddingsPendentesAsync(fonteId, ct);
}
