using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Visão reversa do vínculo usuário↔unidade: gestão dos usuários DE uma unidade
/// (aba "Usuários" no detalhe da unidade). Guardado pelo módulo de Unidades.
/// </summary>
[ApiController]
[Route("unidades/{unidadeId:guid}/usuarios")]
public sealed class UnidadeUsuariosController(IUsuarioUnidadeService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UsuarioDaUnidadeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<UsuarioDaUnidadeDto>> Listar(Guid unidadeId, CancellationToken ct) =>
        await service.ListarUsuariosDaUnidadeAsync(unidadeId, ct);

    [HttpPost("{usuarioId:guid}")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Adicionar(Guid unidadeId, Guid usuarioId, CancellationToken ct)
    {
        await service.AdicionarUsuarioAsync(unidadeId, usuarioId, ct);
        return NoContent();
    }

    [HttpDelete("{usuarioId:guid}")]
    [RequerPermissao(ModuloPermissao.Unidades, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(Guid unidadeId, Guid usuarioId, CancellationToken ct)
    {
        await service.RemoverUsuarioAsync(unidadeId, usuarioId, ct);
        return NoContent();
    }
}
