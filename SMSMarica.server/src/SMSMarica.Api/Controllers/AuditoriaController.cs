using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Auditoria.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

[ApiController]
[Route("auditoria")]
public sealed class AuditoriaController(IAuditoriaService auditoria) : ControllerBase
{
    private readonly IAuditoriaService _auditoria = auditoria;

    /// <summary>
    /// Busca na trilha de auditoria do sistema (ações de usuário), mais recentes
    /// primeiro. Filtros opcionais: entidade, id da entidade, usuário, período e
    /// texto livre (bate em valor anterior/novo, nome do usuário e ação).
    /// </summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Auditoria, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaAuditoriaDto>(StatusCodes.Status200OK)]
    public async Task<PaginaAuditoriaDto> Buscar(
        [FromQuery] string? entidade,
        [FromQuery] string? entidadeId,
        [FromQuery] Guid? usuarioId,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken cancellationToken = default) =>
        await _auditoria.BuscarAsync(
            new AuditoriaFiltroDto(entidade, entidadeId, usuarioId, texto, de, ate, pagina, tamanho),
            cancellationToken);
}
