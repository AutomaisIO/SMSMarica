using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Associacoes;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Correção de IDENTIDADE de exame — o estudo está no paciente errado.
///
/// <para>Módulo de permissão próprio (<see cref="ModuloPermissao.CorrecaoIdentidadeExame"/>) e não
/// uma ação do PACS: a operação <b>reescreve o objeto DICOM</b> (apaga o original e re-armazena
/// com UIDs novos), descarta rascunhos de laudo e revoga o link já enviado ao paciente. É para um
/// punhado de nomes, não para todo perfil que mexe em exames.</para>
/// </summary>
[ApiController]
[Route("correcao-identidade")]
public sealed class CorrecaoIdentidadeExameController(ICorrecaoIdentidadeExameService service) : ControllerBase
{
    /// <summary>
    /// Prévia da correção: quem tem o estudo hoje, para quem vai, e o que será perdido no caminho
    /// (rascunhos, link enviado). Sem <paramref name="accessionDestino"/>, devolve só o estado atual.
    /// </summary>
    [HttpGet("previa/{studyInstanceUID}")]
    // Edicao, não Consulta: a prévia só é pedida de dentro do modal, por quem vai corrigir.
    // Exigir Consulta aqui daria 401 em quem tem só a permissão de executar.
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<PreviaCorrecaoDto>(StatusCodes.Status200OK)]
    public async Task<PreviaCorrecaoDto> Previa(
        string studyInstanceUID, [FromQuery] string? accessionDestino, CancellationToken cancellationToken) =>
        await service.ObterPreviaAsync(studyInstanceUID, accessionDestino, cancellationToken);

    /// <summary>Opção 1 — o estudo é lixo: rejeita (IOCM 113038), apaga do PACS e o exame volta
    /// à worklist para ser refeito.</summary>
    [HttpPost("descartar")]
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Descartar(
        [FromBody] DescartarEstudoRequest request, CancellationToken cancellationToken)
    {
        await service.DescartarEstudoAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Opção 2 — o estudo é de outra pessoa: reescreve para a solicitação de destino e
    /// libera a origem (devolvendo-a à worklist, ou não, conforme ela já tenha feito o exame).</summary>
    [HttpPost("alterar-destino")]
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarDestino(
        [FromBody] AlterarDestinoRequest request, CancellationToken cancellationToken)
    {
        await service.AlterarDestinoAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Opção 3 — cada estudo está no exame do outro: reescreve os dois de uma vez.
    /// Ninguém volta à worklist, porque os dois exames foram feitos.</summary>
    [HttpPost("trocar")]
    [RequerPermissao(ModuloPermissao.CorrecaoIdentidadeExame, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Trocar(
        [FromBody] TrocarEstudosRequest request, CancellationToken cancellationToken)
    {
        await service.TrocarAsync(request, cancellationToken);
        return NoContent();
    }
}
