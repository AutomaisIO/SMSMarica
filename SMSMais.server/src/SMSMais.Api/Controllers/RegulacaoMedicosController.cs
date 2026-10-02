using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Medicos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Médico pedido na abertura da solicitação que ainda não está na lista do sistema de destino.
///
/// <para>Quem pode abrir solicitação pode procurar e pedir o médico; o pedido fica PENDENTE.
/// Quem cadastra no SER/SERNIT é o técnico da regulação, pela tela do próprio sistema, e confirma
/// aqui — nada desta API escreve no sistema do Estado.</para>
/// </summary>
[ApiController]
[Route("regulacao/medicos")]
public sealed class RegulacaoMedicosController(IRegulacaoMedicoPendenteService medicos) : ControllerBase
{
    /// <summary>"Já existe?" — parecidos na lista do sistema e entre os já pedidos.</summary>
    [HttpGet("parecidos")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MedicoParecidoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MedicoParecidoDto>> Parecidos(
        [FromQuery] SistemaRegulacao sistema, [FromQuery] string? nome, [FromQuery] string? documento,
        CancellationToken cancellationToken) =>
        medicos.ParecidosAsync(sistema, nome ?? string.Empty, documento, cancellationToken);

    /// <summary>Pede o cadastro do médico — fica pendente até a regulação confirmar.</summary>
    [HttpPost("pendentes")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<MedicoPendenteDto>(StatusCodes.Status200OK)]
    public Task<MedicoPendenteDto> Criar([FromBody] CriarMedicoPendenteRequest req, CancellationToken cancellationToken) =>
        medicos.CriarAsync(req, cancellationToken);

    [HttpGet("pendentes")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MedicoPendenteDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MedicoPendenteDto>> Listar(
        [FromQuery] SistemaRegulacao? sistema, [FromQuery] SituacaoMedicoPendente? situacao,
        CancellationToken cancellationToken) =>
        medicos.ListarAsync(sistema, situacao, cancellationToken);

    [HttpGet("pendentes/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Regulacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<MedicoPendenteDto>(StatusCodes.Status200OK)]
    public Task<MedicoPendenteDto> Obter(Guid id, CancellationToken cancellationToken) =>
        medicos.ObterAsync(id, cancellationToken);

    /// <summary>O técnico da regulação: cadastrei no sistema / já existia / recusado.</summary>
    [HttpPost("pendentes/{id:guid}/resolver")]
    [RequerPermissao(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
    [ProducesResponseType<MedicoPendenteDto>(StatusCodes.Status200OK)]
    public Task<MedicoPendenteDto> Resolver(
        Guid id, [FromBody] ResolverMedicoPendenteRequest req, CancellationToken cancellationToken) =>
        medicos.ResolverAsync(id, req, cancellationToken);
}
