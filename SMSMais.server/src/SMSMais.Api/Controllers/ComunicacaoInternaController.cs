using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.ComunicacaoInterna;
using SMSMais.Core.ComunicacaoInterna.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Comunicação interna (ADR-0079): chat entre unidades e entre pessoas, dentro do painel (balão
/// laranja). Ler = <see cref="ModuloPermissao.ComunicacaoInterna"/> Consulta; enviar e abrir conversa
/// = Inclusão. Toda leitura é recortada pela participação no serviço (conversa alheia = 404).
/// </summary>
[ApiController]
[Route("comunicacao-interna")]
public sealed class ComunicacaoInternaController(IComunicacaoInternaService service) : ControllerBase
{
    /// <summary>Margem do multipart sobre o teto do anexo (texto + cabeçalhos).</summary>
    private const long TetoRequisicao = ComunicacaoInternaService.TamanhoMaximoAnexoBytes + 1024 * 1024;

    [HttpGet("contexto")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<ContextoComunicacaoInternaDto>(StatusCodes.Status200OK)]
    public Task<ContextoComunicacaoInternaDto> Contexto(CancellationToken ct) => service.ObterContextoAsync(ct);

    /// <summary>Total de não-lidas (balão laranja). Conversa silenciada não conta.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoComunicacaoInternaDto>(StatusCodes.Status200OK)]
    public Task<ResumoComunicacaoInternaDto> Resumo(CancellationToken ct) => service.ObterResumoAsync(ct);

    [HttpGet("conversas")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ConversaInternaItemDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConversaInternaItemDto>> Conversas(CancellationToken ct) => service.ListarConversasAsync(ct);

    [HttpGet("conversas/{id:guid}")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConversaInternaDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ConversaInternaDetalheDto> Conversa(Guid id, CancellationToken ct) => service.ObterConversaAsync(id, ct);

    /// <summary>Abre (ou reabre) a conversa "Todos" entre a minha unidade e a de destino.</summary>
    [HttpPost("conversas/unidade")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ConversaInternaItemDto>(StatusCodes.Status200OK)]
    public Task<ConversaInternaItemDto> AbrirUnidade([FromBody] AbrirConversaUnidadeRequest req, CancellationToken ct) =>
        service.AbrirConversaUnidadeAsync(req, ct);

    /// <summary>Abre (ou reabre) a conversa direta com uma pessoa.</summary>
    [HttpPost("conversas/direta")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ConversaInternaItemDto>(StatusCodes.Status200OK)]
    public Task<ConversaInternaItemDto> AbrirDireta([FromBody] AbrirConversaDiretaRequest req, CancellationToken ct) =>
        service.AbrirConversaDiretaAsync(req, ct);

    /// <summary>Página de mensagens em ordem cronológica; <c>antesDe</c> busca as anteriores.</summary>
    [HttpGet("conversas/{id:guid}/mensagens")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaMensagensInternasDto>(StatusCodes.Status200OK)]
    public Task<PaginaMensagensInternasDto> Mensagens(
        Guid id, [FromQuery] DateTime? antesDe, [FromQuery] int limite = 50, CancellationToken ct = default) =>
        service.ListarMensagensAsync(id, antesDe, limite, ct);

    /// <summary>Envia texto e/ou UM arquivo (multipart). Áudio gravado vai com <c>ehAudio=true</c> e a duração.</summary>
    [HttpPost("conversas/{id:guid}/mensagens")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Inclusao)]
    [RequestSizeLimit(TetoRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = TetoRequisicao)]
    [ProducesResponseType<MensagemInternaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<MensagemInternaDto> Enviar(
        Guid id,
        [FromForm] string? texto,
        IFormFile? arquivo,
        [FromForm] int? duracaoSegundos,
        [FromForm] bool ehAudio,
        [FromForm] long? tamanhoBytes,
        CancellationToken ct)
    {
        AnexoEnviadoInterno? anexo = null;
        if (arquivo is not null)
        {
            using var ms = new MemoryStream();
            await arquivo.CopyToAsync(ms, ct);
            anexo = new AnexoEnviadoInterno(
                arquivo.FileName, arquivo.ContentType, ms.ToArray(), duracaoSegundos, ehAudio, tamanhoBytes);
        }
        return await service.EnviarAsync(id, texto, anexo, ct);
    }

    [HttpPost("conversas/{id:guid}/lida")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarLida(Guid id, CancellationToken ct)
    {
        await service.MarcarLidaAsync(id, ct);
        return NoContent();
    }

    [HttpPut("conversas/{id:guid}/silenciar")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Silenciar(Guid id, [FromBody] SilenciarConversaInternaRequest req, CancellationToken ct)
    {
        await service.SilenciarAsync(id, req.Silenciada, ct);
        return NoContent();
    }

    /// <summary>
    /// Conteúdo do anexo, só para quem participa da conversa. Fora de imagem/áudio/PDF sai como
    /// octet-stream (ver <see cref="ComunicacaoInternaService"/>) e sempre como download nominal.
    /// </summary>
    [HttpGet("mensagens/{id:guid}/anexo")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Anexo(Guid id, CancellationToken ct)
    {
        var anexo = await service.ObterAnexoAsync(id, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(anexo.Conteudo, anexo.Mime, anexo.Nome);
    }

    /// <summary>
    /// Transcreve o áudio da mensagem (ElevenLabs, a mesma dos áudios do WhatsApp) e guarda o texto nela;
    /// se alguém já pediu, devolve o que está guardado. Quem participa da conversa pode pedir.
    /// </summary>
    [HttpPost("mensagens/{id:guid}/transcricao")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<MensagemInternaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<MensagemInternaDto> Transcrever(Guid id, CancellationToken ct) => service.TranscreverAsync(id, ct);

    [HttpGet("unidades")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeInternaDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UnidadeInternaDto>> Unidades(CancellationToken ct) => service.ListarUnidadesAsync(ct);

    [HttpGet("unidades/{id:guid}/usuarios")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UsuarioInternoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UsuarioInternoDto>> UsuariosDaUnidade(Guid id, CancellationToken ct) =>
        service.ListarUsuariosDaUnidadeAsync(id, ct);

    [HttpGet("busca")]
    [RequerPermissao(ModuloPermissao.ComunicacaoInterna, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ResultadoBuscaInternaDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ResultadoBuscaInternaDto>> Buscar([FromQuery] string? q, CancellationToken ct) =>
        service.BuscarAsync(q, ct);
}
