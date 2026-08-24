using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Institucional;
using SMSMais.Core.Institucional.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Identidade da instituição desta instância (singleton) — ADR-0043: nome da secretaria,
/// marca, domínios e contatos legais.
///
/// <para>
/// A <b>leitura</b> pública vive em <c>GET /publico/instituicao</c>, sem autenticação, porque o
/// painel e os PWAs precisam da marca antes de haver sessão. Aqui ficam a leitura administrativa
/// e a escrita, ambas atrás de permissão.
/// </para>
/// </summary>
[ApiController]
[Route("instituicao")]
public sealed class InstituicaoController(IInstituicaoService service) : ControllerBase
{
    private readonly IInstituicaoService _service = service;

    /// <summary>Obtém a identidade configurada (padrão neutro se ainda não houver registro).</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Instituicao, AcoesPermissao.Consulta)]
    [ProducesResponseType<InstituicaoDto>(StatusCodes.Status200OK)]
    public async Task<InstituicaoDto> Obter(CancellationToken cancellationToken) =>
        await _service.ObterAsync(cancellationToken);

    /// <summary>Salva (upsert) a identidade da instituição.</summary>
    [HttpPut]
    [RequerPermissao(ModuloPermissao.Instituicao, AcoesPermissao.Edicao)]
    [ProducesResponseType<InstituicaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<InstituicaoDto> Salvar(
        [FromBody] SalvarInstituicaoRequest request,
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
