using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMarica.Api.Auth;
using SMSMais.Core.Anexos;
using SMSMais.Core.Anexos.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Anexos de exame — ponte QR → PWA "Arquivos Saúde Maricá". O médico gera um
/// token de upload na tela de Anamnese (QR code); o cidadão digitaliza o documento
/// no celular (PWA anônimo) e o envia; o médico revisa e salva. Documentos salvos
/// entram no histórico do paciente. Endpoints do médico são autenticados (mesma
/// permissão da Anamnese); os endpoints <c>/anexos/sessao/*</c> do PWA são anônimos
/// (só o token autoriza), com CORS e rate-limit dedicados.
/// </summary>
[ApiController]
public sealed class AnexosController(IAnexosService service) : ControllerBase
{
    private readonly IAnexosService _service = service;

    // =====================================================================
    // Lado médico (autenticado — JWT do front; permissão da Anamnese)
    // =====================================================================

    /// <summary>Cria um token de upload (escopo de 1 solicitação) para o QR code.</summary>
    [HttpPost("anamneses/{solicitacaoExameId:guid}/anexos/tokens")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<CriarTokenRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<CriarTokenRespostaDto> CriarToken(
        Guid solicitacaoExameId, CancellationToken cancellationToken) =>
        await _service.CriarTokenAsync(solicitacaoExameId, cancellationToken);

    /// <summary>Lista os documentos anexados a uma solicitação (todos os status, não-excluídos).</summary>
    [HttpGet("anamneses/{solicitacaoExameId:guid}/anexos")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AnexoExameDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AnexoExameDto>> ListarPorSolicitacao(
        Guid solicitacaoExameId, CancellationToken cancellationToken) =>
        await _service.ListarPorSolicitacaoAsync(solicitacaoExameId, cancellationToken);

    /// <summary>Confirma/atualiza um documento (Pendente → Salvo).</summary>
    [HttpPost("anexos/{id:guid}/salvar")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<AnexoExameDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AnexoExameDto> Salvar(
        Guid id, [FromBody] SalvarAnexoDto dto, CancellationToken cancellationToken) =>
        await _service.SalvarAsync(id, dto, cancellationToken);

    /// <summary>Exclusão lógica (soft delete) de um documento anexado.</summary>
    [HttpDelete("anexos/{id:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken cancellationToken)
    {
        await _service.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Stream do PDF de um documento (autenticado).</summary>
    [HttpGet("anexos/{id:guid}/conteudo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterConteudo(Guid id, CancellationToken cancellationToken)
    {
        var conteudo = await _service.ObterConteudoAsync(id, cancellationToken);
        if (conteudo is null) return NotFound();
        return File(conteudo.Conteudo, conteudo.MimeType, conteudo.NomeArquivo);
    }

    /// <summary>Histórico do paciente: documentos Salvos, agregados via SolicitacaoExame.PacienteId.</summary>
    [HttpGet("pacientes/{id:guid}/anexos-exame")]
    [RequerPermissao(ModuloPermissao.Pacientes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AnexoExameDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AnexoExameDto>> ListarPorPaciente(
        Guid id, CancellationToken cancellationToken) =>
        await _service.ListarPorPacienteAsync(id, cancellationToken);

    // =====================================================================
    // Lado PWA (anônimo — só o token autoriza; isento de JWT/consentimento)
    // =====================================================================

    /// <summary>Valida a sessão de upload pelo token (mostra o nome do paciente). 404 se inválido/expirado/revogado.</summary>
    [HttpGet("anexos/sessao/{token}")]
    [AllowAnonymous]
    [EnableCors("arquivos-pwa")]
    [EnableRateLimiting("anexos-sessao")]
    [ProducesResponseType<ValidarTokenRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ValidarTokenRespostaDto> ValidarSessao(
        string token, CancellationToken cancellationToken) =>
        await _service.ValidarTokenAsync(token, cancellationToken);

    /// <summary>Recebe um PDF digitalizado no PWA (multipart). Cria o documento Pendente. Não consome o token (multi-uso no TTL).</summary>
    [HttpPost("anexos/sessao/{token}")]
    [AllowAnonymous]
    [EnableCors("arquivos-pwa")]
    [EnableRateLimiting("anexos-sessao")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    [ProducesResponseType<AnexoUploadRespostaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceberUpload(
        string token,
        IFormFile arquivo,
        [FromForm] string nome,
        [FromForm] string? descricao,
        [FromForm] int? paginas,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, cancellationToken);

        var resultado = await _service.ReceberUploadAsync(
            token,
            nome,
            descricao,
            paginas,
            ms.ToArray(),
            arquivo.ContentType,
            cancellationToken);

        return CreatedAtAction(nameof(ObterConteudo), new { id = resultado.Id }, resultado);
    }
}
