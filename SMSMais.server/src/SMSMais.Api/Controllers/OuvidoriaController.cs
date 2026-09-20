using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SMSMais.Api.Auth;
using SMSMais.Core.Identidade;
using SMSMais.Core.Midias;
using SMSMais.Core.Midias.Dtos;
using SMSMais.Core.Ouvidoria;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Ouvidoria (ADR-0060): fila e máquina de estados da manifestação, catálogo (pontos de resposta,
/// assuntos, marcadores), configuração e painel. Gate por action em quatro módulos ortogonais:
/// <see cref="ModuloPermissao.Ouvidoria"/> (trabalho da ouvidoria), <see cref="ModuloPermissao.OuvidoriaGestao"/>
/// (catálogo, configuração, painel, escalonamento), <see cref="ModuloPermissao.OuvidoriaSigilo"/>
/// (denúncia e identidade) e <see cref="ModuloPermissao.OuvidoriaPontoResposta"/> (quem responde pela área).
/// A visibilidade fina (manifestante mascarado, escopo do ponto) é decidida no service.
/// </summary>
[ApiController]
[Route("ouvidoria")]
public sealed class OuvidoriaController(
    IOuvidoriaManifestacaoService manifestacoes,
    IOuvidoriaCatalogoService catalogo,
    IMidiasService midias,
    IIdentidadeService identidade) : ControllerBase
{
    // ---------------- Manifestações: consulta ----------------

    [HttpGet("manifestacoes")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaPontoResposta)]
    [ProducesResponseType<PaginaDto<ManifestacaoListaDto>>(StatusCodes.Status200OK)]
    public async Task<PaginaDto<ManifestacaoListaDto>> Listar([FromQuery] ManifestacaoFiltro filtro, CancellationToken ct)
        => await manifestacoes.ListarAsync(filtro, ct);

    [HttpGet("manifestacoes/resumo")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaPontoResposta)]
    [ProducesResponseType<OuvidoriaResumoDto>(StatusCodes.Status200OK)]
    public async Task<OuvidoriaResumoDto> Resumo(CancellationToken ct) => await manifestacoes.ObterResumoAsync(ct);

    [HttpGet("manifestacoes/{id:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaPontoResposta)]
    [ProducesResponseType<ManifestacaoDetalheDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ManifestacaoDetalheDto> Obter(Guid id, CancellationToken ct) => await manifestacoes.ObterAsync(id, ct);

    // ---------------- Manifestações: registro ----------------

    /// <summary>Registra a manifestação. O código de acesso volta <b>só aqui</b>, uma única vez.</summary>
    [HttpPost("manifestacoes")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Inclusao)]
    [ProducesResponseType<ManifestacaoCriadaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarManifestacaoRequest req, CancellationToken ct)
    {
        var criado = await manifestacoes.RegistrarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    // ---------------- Manifestações: ações (Ouvidoria.Edicao) ----------------

    [HttpPost("manifestacoes/{id:guid}/triagem")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Triar(Guid id, [FromBody] TriarRequest req, CancellationToken ct)
    {
        await manifestacoes.TriarAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/encaminhar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Encaminhar(Guid id, [FromBody] EncaminharRequest req, CancellationToken ct)
    {
        await manifestacoes.EncaminharAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/pedir-complementacao")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PedirComplementacao(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.PedirComplementacaoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/complementar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complementar(Guid id, [FromBody] TextoComAnexosRequest req, CancellationToken ct)
    {
        await manifestacoes.ComplementarAsync(id, req, ct);
        return NoContent();
    }

    /// <summary>Resposta da área: membro do ponto de resposta ou técnico da ouvidoria.</summary>
    [HttpPost("manifestacoes/{id:guid}/responder-area")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.OuvidoriaPontoResposta, ModuloPermissao.Ouvidoria)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResponderArea(Guid id, [FromBody] TextoComAnexosRequest req, CancellationToken ct)
    {
        await manifestacoes.ResponderAreaAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/devolver-area")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DevolverArea(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.DevolverParaReanaliseAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/responder-cidadao")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResponderCidadao(Guid id, [FromBody] ResponderCidadaoRequest req, CancellationToken ct)
    {
        await manifestacoes.ResponderCidadaoAsync(id, req, ct);
        return NoContent();
    }

    /// <summary>Prorroga uma única vez (Lei 13.460, art. 16 §1º). O texto é a justificativa (≥ 20 caracteres).</summary>
    [HttpPost("manifestacoes/{id:guid}/prorrogar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Prorrogar(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.ProrrogarAsync(id, req, ct);
        return NoContent();
    }

    /// <summary>Cobra a área. Corpo opcional; sem texto, registra a cobrança padrão com o prazo.</summary>
    [HttpPost("manifestacoes/{id:guid}/cobrar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cobrar(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TextoRequest? req, CancellationToken ct)
    {
        await manifestacoes.CobrarAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/escalonar")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Escalonar(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.EscalonarAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/recurso")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recurso(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.RegistrarRecursoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/concluir")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Concluir(Guid id, CancellationToken ct)
    {
        await manifestacoes.ConcluirAsync(id, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/arquivar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Arquivar(Guid id, [FromBody] ArquivarRequest req, CancellationToken ct)
    {
        await manifestacoes.ArquivarAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/encaminhar-externo")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EncaminharExterno(Guid id, [FromBody] EncaminharExternoRequest req, CancellationToken ct)
    {
        await manifestacoes.EncaminharExternoAsync(id, req, ct);
        return NoContent();
    }

    // ---------------- Denúncia / sigilo ----------------

    /// <summary>Juízo de admissibilidade da denúncia (autoria, materialidade, competência).</summary>
    [HttpPost("manifestacoes/{id:guid}/habilitar")]
    [RequerPermissao(ModuloPermissao.OuvidoriaSigilo, AcoesPermissao.Inclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Habilitar(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.HabilitarDenunciaAsync(id, req, ct);
        return NoContent();
    }

    [HttpPut("manifestacoes/{id:guid}/teor-pseudonimizado")]
    [RequerPermissao(ModuloPermissao.OuvidoriaSigilo, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarTeorPseudonimizado(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.AtualizarTeorPseudonimizadoAsync(id, req, ct);
        return NoContent();
    }

    [HttpPost("manifestacoes/{id:guid}/anotar")]
    [RequerPermissao(ModuloPermissao.Ouvidoria, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Anotar(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
    {
        await manifestacoes.AnotarAsync(id, req, ct);
        return NoContent();
    }

    /// <summary>
    /// Revela a identidade do manifestante de sigilosa/denúncia. O texto é a justificativa
    /// (≥ 15 caracteres); o acesso fica registrado (Decreto 10.153, art. 6º §3º).
    /// </summary>
    [HttpPost("manifestacoes/{id:guid}/identidade")]
    [RequerPermissao(ModuloPermissao.OuvidoriaSigilo, AcoesPermissao.Consulta)]
    [ProducesResponseType<ManifestanteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ManifestanteDto> RevelarIdentidade(Guid id, [FromBody] TextoRequest req, CancellationToken ct)
        => await manifestacoes.RevelarIdentidadeAsync(id, req.Texto, ct);

    // ---------------- Anexos ----------------

    /// <summary>
    /// Envia um arquivo e devolve a mídia para referenciar no registro, complementação ou resposta
    /// da área. Aceita <c>Ouvidoria.Inclusao</c> <b>ou</b> <c>OuvidoriaPontoResposta.Edicao</c> — como
    /// são ações diferentes em módulos diferentes, a checagem é feita aqui (os atributos só
    /// combinam módulos com a mesma ação).
    /// </summary>
    [HttpPost("anexos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<MidiaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EnviarAnexo(IFormFile arquivo, CancellationToken ct)
    {
        var usuarioId = ExtrairUsuarioId();
        if (!await PodeAnexarAsync(usuarioId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Permissão negada.",
                Detail = "Anexar exige 'Inclusao' em 'Ouvidoria' ou 'Edicao' em 'OuvidoriaPontoResposta'.",
                Type = "permissao.negada",
            });
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        var dto = await midias.EnviarAsync(usuarioId, arquivo.FileName, arquivo.ContentType, ms.ToArray(), "ouvidoria", ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    private async Task<bool> PodeAnexarAsync(Guid? usuarioId, CancellationToken ct)
    {
        // Token de serviço (X-API-Key) tem acesso pleno, como nos atributos de permissão.
        if (User.FindFirstValue(ApiKeyAuthenticationHandler.ClaimTokenType) == ApiKeyAuthenticationHandler.ValorServico) return true;
        if (usuarioId is null) return false;
        try
        {
            var perms = await identidade.ObterPermissoesResolvidasAsync(usuarioId.Value, ct);
            return perms.Resolvidas.Any(p =>
                (p.Modulo == ModuloPermissao.Ouvidoria && p.Acoes.HasFlag(AcoesPermissao.Inclusao))
                || (p.Modulo == ModuloPermissao.OuvidoriaPontoResposta && p.Acoes.HasFlag(AcoesPermissao.Edicao)));
        }
        catch (SMSMais.Core.Common.Excecoes.NaoEncontradoException)
        {
            return false;
        }
    }

    // ---------------- Pontos de resposta ----------------

    [HttpGet("pontos-resposta")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaGestao)]
    [ProducesResponseType<IReadOnlyList<PontoRespostaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PontoRespostaDto>> ListarPontosResposta(CancellationToken ct)
        => await catalogo.ListarPontosRespostaAsync(ct);

    [HttpGet("pontos-resposta/{id:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaGestao)]
    [ProducesResponseType<PontoRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PontoRespostaDto> ObterPontoResposta(Guid id, CancellationToken ct)
        => await catalogo.ObterPontoRespostaAsync(id, ct);

    [HttpPost("pontos-resposta")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<PontoRespostaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarPontoResposta([FromBody] SalvarPontoRespostaRequest req, CancellationToken ct)
    {
        var criado = await catalogo.CriarPontoRespostaAsync(req, ct);
        return CreatedAtAction(nameof(ObterPontoResposta), new { id = criado.Id }, criado);
    }

    [HttpPut("pontos-resposta/{id:guid}")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarPontoResposta(Guid id, [FromBody] SalvarPontoRespostaRequest req, CancellationToken ct)
    {
        await catalogo.AtualizarPontoRespostaAsync(id, req, ct);
        return NoContent();
    }

    // ---------------- Assuntos (leitura: qualquer autenticado) ----------------

    [HttpGet("assuntos")]
    [ProducesResponseType<IReadOnlyList<AssuntoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<AssuntoDto>> ListarAssuntos(CancellationToken ct) => await catalogo.ListarAssuntosAsync(ct);

    [HttpPost("assuntos")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<AssuntoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarAssunto([FromBody] SalvarAssuntoRequest req, CancellationToken ct)
    {
        var criado = await catalogo.CriarAssuntoAsync(req, ct);
        return StatusCode(StatusCodes.Status201Created, criado);
    }

    [HttpPut("assuntos/{id:guid}")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarAssunto(Guid id, [FromBody] SalvarAssuntoRequest req, CancellationToken ct)
    {
        await catalogo.AtualizarAssuntoAsync(id, req, ct);
        return NoContent();
    }

    // ---------------- Marcadores (leitura: qualquer autenticado) ----------------

    [HttpGet("marcadores")]
    [ProducesResponseType<IReadOnlyList<MarcadorDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MarcadorDto>> ListarMarcadores(CancellationToken ct) => await catalogo.ListarMarcadoresAsync(ct);

    [HttpPost("marcadores")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Inclusao)]
    [ProducesResponseType<MarcadorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarMarcador([FromBody] SalvarMarcadorRequest req, CancellationToken ct)
    {
        var criado = await catalogo.CriarMarcadorAsync(req, ct);
        return StatusCode(StatusCodes.Status201Created, criado);
    }

    [HttpPut("marcadores/{id:guid}")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarMarcador(Guid id, [FromBody] SalvarMarcadorRequest req, CancellationToken ct)
    {
        await catalogo.AtualizarMarcadorAsync(id, req, ct);
        return NoContent();
    }

    // ---------------- Configuração e painel ----------------

    [HttpGet("configuracao")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Ouvidoria, ModuloPermissao.OuvidoriaGestao)]
    [ProducesResponseType<OuvidoriaConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<OuvidoriaConfiguracaoDto> ObterConfiguracao(CancellationToken ct) => await catalogo.ObterConfiguracaoAsync(ct);

    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SalvarConfiguracao([FromBody] OuvidoriaConfiguracaoDto req, CancellationToken ct)
    {
        await catalogo.SalvarConfiguracaoAsync(req, ct);
        return NoContent();
    }

    /// <summary>Indicadores do período (padrão: últimos 30 dias). Datas no fuso da instância.</summary>
    [HttpGet("painel")]
    [RequerPermissao(ModuloPermissao.OuvidoriaGestao, AcoesPermissao.Consulta)]
    [ProducesResponseType<OuvidoriaPainelDto>(StatusCodes.Status200OK)]
    public async Task<OuvidoriaPainelDto> Painel([FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, [FromQuery] Guid? unidadeId, CancellationToken ct)
        => await manifestacoes.ObterPainelAsync(de, ate, unidadeId, ct);

    private Guid? ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
