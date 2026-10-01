using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SMSMais.Api.Auth;
using SMSMais.Core.Extensao.Distribuicao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Distribuição da extensão do Chrome e do atualizador pela plataforma (ADR-0064). Tudo fica
/// hospedado aqui — nada em repositório público — e só sai para quem está logado no painel ou
/// para computador autorizado.
/// <para>
/// Três públicos nas mesmas rotas <c>/extensao</c>: quem está logado (instalador, autorizar um
/// computador), o computador (anônimo para pedir autorização; depois com o token DELE no
/// <c>Authorization: Bearer</c>) e a administração (módulo <see cref="ModuloPermissao.ExtensaoNavegador"/>).
/// </para>
/// </summary>
[ApiController]
[Route("extensao")]
public sealed class ExtensaoDistribuicaoController(
    IExtensaoDistribuicaoService distribuicao,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Limite de pedidos por IP das rotas que o computador chama sem login do painel.</summary>
    public const string PoliticaDeLimite = "extensao-computador";

    // ------------------------------------------------------------ painel: qualquer usuário logado

    /// <summary>O que há publicado para baixar (versão do instalador e da extensão em produção).</summary>
    [HttpGet("situacao")]
    public Task<SituacaoDistribuicaoDto> Situacao(CancellationToken cancellationToken) =>
        distribuicao.ObterSituacaoAsync(cancellationToken);

    /// <summary>
    /// O instalador para quem está logado: o atualizador + o endereço desta API e um código de
    /// ativação de uso único, já autorizado. A pessoa baixa, executa e o computador fica ligado.
    /// </summary>
    [HttpGet("instalador")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Instalador(CancellationToken cancellationToken)
    {
        var arquivo = await distribuicao.GerarInstaladorAsync(EnderecoPublicoDaApi(), cancellationToken);
        // Cada download leva um código próprio: não pode ser servido de cache para outra pessoa.
        Response.Headers.CacheControl = "no-store";
        return File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    /// <summary>O pedido de autorização que a página do painel mostra (qual computador está pedindo).</summary>
    [HttpGet("dispositivos/ativacoes/{codigoPublico}")]
    public Task<AtivacaoPendenteDto> Ativacao(string codigoPublico, CancellationToken cancellationToken) =>
        distribuicao.ObterAtivacaoAsync(codigoPublico, cancellationToken);

    /// <summary>Quem está logado autoriza o computador que pediu.</summary>
    [HttpPost("dispositivos/ativacoes/{codigoPublico}/autorizar")]
    public Task<AtivacaoPendenteDto> Autorizar(string codigoPublico, CancellationToken cancellationToken) =>
        distribuicao.AutorizarAtivacaoAsync(codigoPublico, cancellationToken);

    // ------------------------------------------------------------------- computador: sem login

    /// <summary>O atualizador pede um código e a página do painel onde alguém logado autoriza.</summary>
    [HttpPost("dispositivos/ativacoes")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public Task<AtivacaoIniciadaDto> IniciarAtivacao(
        [FromBody] IniciarAtivacaoRequest request, CancellationToken cancellationToken) =>
        distribuicao.IniciarAtivacaoAsync(request, cancellationToken);

    /// <summary>
    /// Troca o código de ativação pelo token do computador: 200 autorizado · 202 ainda aguardando ·
    /// 410 código desconhecido, usado ou vencido.
    /// </summary>
    [HttpPost("dispositivos/ativacoes/token")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> TrocarCodigo(
        [FromBody] TrocarCodigoRequest request, CancellationToken cancellationToken)
    {
        var troca = await distribuicao.TrocarCodigoAsync(request, cancellationToken);
        return troca.Estado switch
        {
            EstadoDaTroca.Autorizado => Ok(new { token = troca.Token }),
            EstadoDaTroca.Aguardando => StatusCode(StatusCodes.Status202Accepted),
            _ => StatusCode(StatusCodes.Status410Gone),
        };
    }

    // --------------------------------------------------------------- computador: com o token dele
    // [AllowAnonymous] porque o token do computador não é o JWT do painel: é conferido aqui.

    /// <summary>A versão da extensão que este computador deve ter. Recebe o inventário dele.</summary>
    [HttpGet("versao")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> VersaoDaExtensao(
        [FromQuery] string? instalada,
        [FromQuery] string? atualizador,
        [FromQuery] string? chrome,
        CancellationToken cancellationToken)
    {
        var dispositivo = await distribuicao.AutenticarAsync(TokenDoComputador(), cancellationToken);
        if (dispositivo is null) return Unauthorized();
        var versao = await distribuicao.ConsultarExtensaoAsync(
            dispositivo, new InventarioDoComputador(instalada, atualizador, chrome), cancellationToken);
        return versao is null ? NotFound() : Ok(versao);
    }

    [HttpGet("pacote")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public Task<IActionResult> PacoteDaExtensao(CancellationToken cancellationToken) =>
        BaixarAsync(ExtensaoArtefato.Extensao, cancellationToken);

    [HttpGet("atualizador/versao")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> VersaoDoAtualizador(CancellationToken cancellationToken)
    {
        var dispositivo = await distribuicao.AutenticarAsync(TokenDoComputador(), cancellationToken);
        if (dispositivo is null) return Unauthorized();
        var versao = await distribuicao.ConsultarAtualizadorAsync(dispositivo, cancellationToken);
        return versao is null ? NotFound() : Ok(versao);
    }

    [HttpGet("atualizador/pacote")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public Task<IActionResult> PacoteDoAtualizador(CancellationToken cancellationToken) =>
        BaixarAsync(ExtensaoArtefato.Atualizador, cancellationToken);

    private async Task<IActionResult> BaixarAsync(ExtensaoArtefato artefato, CancellationToken cancellationToken)
    {
        var dispositivo = await distribuicao.AutenticarAsync(TokenDoComputador(), cancellationToken);
        if (dispositivo is null) return Unauthorized();
        var arquivo = await distribuicao.BaixarAsync(dispositivo, artefato, cancellationToken);
        if (arquivo is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(arquivo.Conteudo, arquivo.ContentType, arquivo.NomeArquivo);
    }

    // ------------------------------------------------------------------------- administração

    [HttpGet("dispositivos")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Consulta)]
    public Task<IReadOnlyList<DispositivoDto>> Dispositivos(CancellationToken cancellationToken) =>
        distribuicao.ListarDispositivosAsync(cancellationToken);

    /// <summary>Muda o canal (teste/prod) de um computador — vale na próxima consulta dele.</summary>
    [HttpPut("dispositivos/{id:guid}/canal")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Edicao)]
    public Task<DispositivoDto> DefinirCanal(
        Guid id, [FromBody] DefinirCanalRequest request, CancellationToken cancellationToken) =>
        distribuicao.DefinirCanalAsync(id, request.Canal, cancellationToken);

    /// <summary>Revoga o computador: o token dele deixa de valer.</summary>
    [HttpPost("dispositivos/{id:guid}/revogar")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Exclusao)]
    public Task<DispositivoDto> Revogar(Guid id, CancellationToken cancellationToken) =>
        distribuicao.RevogarAsync(id, cancellationToken);

    [HttpGet("pacotes")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Consulta)]
    public Task<IReadOnlyList<PacoteDto>> Pacotes(CancellationToken cancellationToken) =>
        distribuicao.ListarPacotesAsync(cancellationToken);

    /// <summary>
    /// Publica uma versão (entra no canal de teste). Extensão: o .zip, e a versão é a do manifest.
    /// Atualizador: o executável, com a versão informada.
    /// </summary>
    [HttpPost("pacotes")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Inclusao)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 32 * 1024 * 1024)]
    [ProducesResponseType<PacoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publicar(
        IFormFile? arquivo,
        [FromForm] ExtensaoArtefato artefato,
        [FromForm] string? versao,
        [FromForm] string? notas,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return Ok(await distribuicao.PublicarAsync(artefato, versao, notas, memoria.ToArray(), ct: cancellationToken));
    }

    /// <summary>Promove a versão ao canal de produção: todos os computadores passam a recebê-la.</summary>
    [HttpPost("pacotes/{id:guid}/promover")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Edicao)]
    public Task<PacoteDto> Promover(Guid id, CancellationToken cancellationToken) =>
        distribuicao.PromoverAsync(id, ct: cancellationToken);

    /// <summary>Retira a versão: deixa de ser entregue (quem já recebeu continua com ela).</summary>
    [HttpPost("pacotes/{id:guid}/retirar")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Exclusao)]
    public Task<PacoteDto> Retirar(Guid id, CancellationToken cancellationToken) =>
        distribuicao.RetirarAsync(id, cancellationToken);

    // ------------------------------------------------------- API de publicação: a opção da tela
    // Publicar pelo painel é o caminho normal. A API é uma OPÇÃO que quem administra liga na tela,
    // gerando uma chave: ela serve só para listar, subir e promover versão — nada mais do sistema.

    /// <summary>Há chave de publicação ativa? (o valor em claro nunca volta por aqui)</summary>
    [HttpGet("publicacao/chave")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Consulta)]
    public Task<ChavePublicacaoDto> ChaveDePublicacao(CancellationToken cancellationToken) =>
        distribuicao.ObterChavePublicacaoAsync(cancellationToken);

    /// <summary>Liga a API de publicação: gera a chave (revogando a anterior) e a mostra uma única vez.</summary>
    [HttpPost("publicacao/chave")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Edicao)]
    public Task<ChaveGeradaDto> GerarChaveDePublicacao(CancellationToken cancellationToken) =>
        distribuicao.GerarChavePublicacaoAsync(cancellationToken);

    /// <summary>Desliga a API de publicação: a chave deixa de valer.</summary>
    [HttpDelete("publicacao/chave")]
    [RequerPermissao(ModuloPermissao.ExtensaoNavegador, AcoesPermissao.Edicao)]
    public Task<ChavePublicacaoDto> RevogarChaveDePublicacao(CancellationToken cancellationToken) =>
        distribuicao.RevogarChavePublicacaoAsync(cancellationToken);

    /// <summary>Cabeçalho em que a chave de publicação é enviada.</summary>
    public const string CabecalhoDaChave = "X-Chave-Publicacao";

    [HttpGet("publicacao/pacotes")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> PacotesPelaApi(CancellationToken cancellationToken) =>
        await ChaveValeAsync(cancellationToken)
            ? Ok(await distribuicao.ListarPacotesAsync(cancellationToken))
            : Unauthorized();

    [HttpPost("publicacao/pacotes")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 32 * 1024 * 1024)]
    public async Task<IActionResult> PublicarPelaApi(
        IFormFile? arquivo,
        [FromForm] ExtensaoArtefato artefato,
        [FromForm] string? versao,
        [FromForm] string? notas,
        CancellationToken cancellationToken)
    {
        if (!await ChaveValeAsync(cancellationToken)) return Unauthorized();
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo não enviado." });
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return Ok(await distribuicao.PublicarAsync(
            artefato, versao, notas, memoria.ToArray(), pelaApi: true, ct: cancellationToken));
    }

    [HttpPost("publicacao/pacotes/{id:guid}/promover")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticaDeLimite)]
    public async Task<IActionResult> PromoverPelaApi(Guid id, CancellationToken cancellationToken) =>
        await ChaveValeAsync(cancellationToken)
            ? Ok(await distribuicao.PromoverAsync(id, pelaApi: true, ct: cancellationToken))
            : Unauthorized();

    private Task<bool> ChaveValeAsync(CancellationToken cancellationToken) =>
        distribuicao.ChavePublicacaoValeAsync(Request.Headers[CabecalhoDaChave].ToString(), cancellationToken);

    // -------------------------------------------------------------------------------- miúdos

    private string? TokenDoComputador()
    {
        var cabecalho = Request.Headers.Authorization.ToString();
        const string esquema = "Bearer ";
        return cabecalho.StartsWith(esquema, StringComparison.OrdinalIgnoreCase) ? cabecalho[esquema.Length..].Trim() : null;
    }

    /// <summary>
    /// O endereço que o instalador vai gravar como "a API desta plataforma". Vem da configuração
    /// (<c>Publico:BaseUrl</c>); sem ela, o endereço pelo qual esta requisição chegou.
    /// </summary>
    private string EnderecoPublicoDaApi()
    {
        var configurado = configuration["Publico:BaseUrl"];
        return string.IsNullOrWhiteSpace(configurado)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
            : configurado.TrimEnd('/');
    }
}
