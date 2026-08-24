using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Gestão do vínculo N:N agente↔unidade (base da visibilidade das filas do chat). Guardado pelo
/// módulo de Usuários — é configuração de usuário.
/// </summary>
[ApiController]
[Route("usuarios/{usuarioId:guid}/unidades")]
public sealed class UsuarioUnidadesController(IUsuarioUnidadeService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VinculoUnidadeDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<VinculoUnidadeDto>> Listar(Guid usuarioId, CancellationToken ct) =>
        await service.ObterVinculosAsync(usuarioId, ct);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Definir(Guid usuarioId, [FromBody] DefinirVinculosUnidadeRequest request, CancellationToken ct)
    {
        await service.DefinirVinculosAsync(usuarioId, request, ct);
        return NoContent();
    }
}
