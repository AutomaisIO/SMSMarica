using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Laudos.Configuracao.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Configuração global (singleton) do cabeçalho/rodapé institucional dos laudos.
/// O conteúdo é HTML (editado no painel com a mesma stack do corpo do laudo) e é
/// renderizado no topo/rodapé de cada página do PDF.
/// </summary>
[ApiController]
[Route("laudos/configuracao")]
public sealed class LaudoConfiguracaoController(ILaudoConfiguracaoService service) : ControllerBase
{
    private readonly ILaudoConfiguracaoService _service = service;

    /// <summary>Obtém o cabeçalho/rodapé global atual.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.ConfiguracaoLaudo, AcoesPermissao.Consulta)]
    [ProducesResponseType<LaudoConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<LaudoConfiguracaoDto> Obter(CancellationToken cancellationToken) =>
        await _service.ObterAsync(cancellationToken);

    /// <summary>Salva (upsert) o cabeçalho/rodapé global.</summary>
    [HttpPut]
    [RequerPermissao(ModuloPermissao.ConfiguracaoLaudo, AcoesPermissao.Edicao)]
    [ProducesResponseType<LaudoConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<LaudoConfiguracaoDto> Salvar(
        [FromBody] SalvarLaudoConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = ExtrairUsuarioId();
        return await _service.SalvarAsync(usuarioId, request, cancellationToken);
    }

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}
