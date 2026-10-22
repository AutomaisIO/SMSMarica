using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Exames.ImagensInvalidadas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Imagens de um estudo PACS retiradas do que vai ao paciente. A imagem continua no PACS/S3 e no
/// visualizador; só sai dos PDFs (exame completo, app do cidadão, acervo). Quem pode retirar ou
/// devolver é decidido no serviço (quem lauda / quem retirou / acesso global) — aqui basta enxergar
/// o PACS.
/// </summary>
[ApiController]
[Route("estudos/{studyInstanceUID}/imagens-invalidadas")]
public sealed class ImagensInvalidadasController(IImagensInvalidadasService service) : ControllerBase
{
    /// <summary>O que está fora do estudo e se o usuário pode retirar/devolver.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImagensInvalidadasDoEstudoDto>(StatusCodes.Status200OK)]
    public Task<ImagensInvalidadasDoEstudoDto> Listar(string studyInstanceUID, CancellationToken cancellationToken) =>
        service.ListarAsync(studyInstanceUID, ExtrairUsuarioId(), cancellationToken);

    /// <summary>Retira a imagem do que vai ao paciente e refaz o PDF dele. ESCRITA.</summary>
    [HttpPut("{sopInstanceUID}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImagensInvalidadasDoEstudoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ImagensInvalidadasDoEstudoDto> Invalidar(
        string studyInstanceUID,
        string sopInstanceUID,
        [FromBody] InvalidarImagemRequest? request,
        CancellationToken cancellationToken) =>
        service.InvalidarAsync(studyInstanceUID, sopInstanceUID, ExtrairUsuarioId(), request?.Motivo, cancellationToken);

    /// <summary>Devolve a imagem ao que vai ao paciente e refaz o PDF dele. ESCRITA.</summary>
    [HttpDelete("{sopInstanceUID}")]
    [RequerPermissao(ModuloPermissao.Pacs, AcoesPermissao.Consulta)]
    [ProducesResponseType<ImagensInvalidadasDoEstudoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ImagensInvalidadasDoEstudoDto> Restaurar(
        string studyInstanceUID,
        string sopInstanceUID,
        CancellationToken cancellationToken) =>
        service.RestaurarAsync(studyInstanceUID, sopInstanceUID, ExtrairUsuarioId(), cancellationToken);

    private Guid ExtrairUsuarioId()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id)) return id;
        throw new ValidacaoException("auth.sub_invalido", "Token sem identificação do usuário.");
    }
}
