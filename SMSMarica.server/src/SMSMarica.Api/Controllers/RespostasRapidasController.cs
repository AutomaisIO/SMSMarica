using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Conversas.RespostasRapidas;
using SMSMarica.Core.Conversas.RespostasRapidas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Mensagens prontas do chat ("respostas rápidas"). O CADASTRO exige o módulo
/// <see cref="ModuloPermissao.RespostasRapidas"/>; USAR os atalhos numa conversa exige só
/// <see cref="ModuloPermissao.Conversas"/> — quem atende não precisa poder editar a lista.
/// </summary>
[ApiController]
[Route("respostas-rapidas")]
public sealed class RespostasRapidasController(IRespostaRapidaService service) : ControllerBase
{
    /// <summary>As que o operador enxerga: as globais + as das unidades dele.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RespostaRapidaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RespostaRapidaDto>> Listar(
        [FromQuery] bool incluirInativas = false, CancellationToken ct = default) =>
        await service.ListarAsync(incluirInativas, ct);

    /// <summary>Tags automáticas disponíveis (o cadastro usa sem declarar campo).</summary>
    [HttpGet("tags")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TagAutomaticaDto>>(StatusCodes.Status200OK)]
    public IReadOnlyList<TagAutomaticaDto> Tags() => service.ListarTagsAutomaticas();

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RespostasRapidas, AcoesPermissao.Consulta)]
    [ProducesResponseType<RespostaRapidaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RespostaRapidaDto> Obter(Guid id, CancellationToken ct) =>
        await service.ObterAsync(id, ct);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.RespostasRapidas, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar(
        [FromBody] SalvarRespostaRapidaRequest request, CancellationToken ct)
    {
        var id = await service.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RespostasRapidas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id, [FromBody] SalvarRespostaRapidaRequest request, CancellationToken ct)
    {
        await service.AtualizarAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.RespostasRapidas, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await service.ExcluirAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Monta o texto no contexto da conversa (tags automáticas + valores digitados). O
    /// resultado vai para o campo de digitação do operador — nada é enviado aqui.
    /// </summary>
    [HttpPost("/conversas/{conversaId:guid}/respostas-rapidas/{id:guid}/resolver")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<TextoResolvidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<TextoResolvidoDto> Resolver(
        Guid conversaId,
        Guid id,
        [FromBody] ResolverRespostaRapidaRequest request,
        CancellationToken ct) =>
        await service.ResolverAsync(conversaId, id, request, ct);
}
