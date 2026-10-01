using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Anexos;
using SMSMais.Core.Anexos.Dtos;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Anexos de exame — ponte QR → PWA "Arquivos Saúde Maricá". O médico gera um
/// token de upload na tela de Anamnese (QR code); o cidadão digitaliza o documento
/// no celular (PWA anônimo) e o envia; o médico revisa e salva. Documentos salvos
/// entram no histórico do paciente. Endpoints do médico são autenticados (mesma
/// permissão da Anamnese); os endpoints <c>/anexos/sessao/*</c> do PWA são anônimos
/// (só o token autoriza), com CORS e rate-limit dedicados.
/// </summary>
[ApiController]
public sealed class AnexosController(IAnexosService service, IDocumentosPacienteService acervo) : ControllerBase
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

    /// <summary>Documentos do cadastro do paciente do exame, disponíveis para anexar na anamnese.</summary>
    [HttpGet("anamneses/{solicitacaoExameId:guid}/acervo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ItemAcervoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ItemAcervoDto>> Acervo(
        Guid solicitacaoExameId, CancellationToken cancellationToken)
    {
        var pacienteId = await acervo.PacienteDoExameImagemAsync(solicitacaoExameId, cancellationToken);
        return await acervo.ListarAsync(pacienteId, incluirPendentes: false, cancellationToken);
    }

    /// <summary>Conteúdo de um documento do cadastro do paciente do exame (visualizador).</summary>
    [HttpGet("anamneses/{solicitacaoExameId:guid}/acervo/conteudo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcervoConteudo(
        Guid solicitacaoExameId, [FromQuery] string chave, CancellationToken cancellationToken)
    {
        var pacienteId = await acervo.PacienteDoExameImagemAsync(solicitacaoExameId, cancellationToken);
        var c = await acervo.ObterConteudoPorChaveAsync(pacienteId, chave, cancellationToken);
        return File(c.Conteudo, c.MimeType, c.NomeArquivo);
    }

    /// <summary>Anexa na anamnese um documento que o paciente já tem no cadastro (entra salvo).</summary>
    [HttpPost("anamneses/{solicitacaoExameId:guid}/anexos/do-acervo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<IReadOnlyList<AnexoExameDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AnexoExameDto>> AnexarDoAcervo(
        Guid solicitacaoExameId, [FromBody] RegulacaoExigenciasController.AnexarDoAcervoRequest req,
        CancellationToken cancellationToken)
    {
        await acervo.AnexarNaAnamneseAsync(solicitacaoExameId, req.Chave, cancellationToken);
        return await _service.ListarPorSolicitacaoAsync(solicitacaoExameId, cancellationToken);
    }

    /// <summary>Stream do documento (PDF ou imagem) — autenticado.</summary>
    [HttpGet("anexos/{id:guid}/conteudo")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
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
