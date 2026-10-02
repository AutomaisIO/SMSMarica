using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Regulacao.Medicos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// O cadastro de médicos que é só nosso — hoje, o do SISREG, onde o médico solicitante é texto
/// digitado em cada pedido. Incluir aqui grava direto (no SISREG não há o que cadastrar); a
/// solicitação continua levando CPF e nome como texto.
/// </summary>
[ApiController]
[Route("regulacao/medicos/locais")]
public sealed class RegulacaoMedicosLocaisController(IRegulacaoMedicoLocalService medicos) : ControllerBase
{
    /// <summary>Busca por palavras do nome (também nas outras grafias) ou pelo começo do CPF.</summary>
    [HttpGet]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Regulacao, ModuloPermissao.RegulacaoTriagem,
        ModuloPermissao.Sisreg)]
    [ProducesResponseType<IReadOnlyList<MedicoLocalDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MedicoLocalDto>> Buscar(
        [FromQuery] SistemaRegulacao sistema, [FromQuery] string? termo, [FromQuery] int? limite,
        CancellationToken cancellationToken) =>
        medicos.BuscarAsync(sistema, termo, limite ?? 20, cancellationToken);

    /// <summary>"Já existe?" — antes de incluir.</summary>
    [HttpGet("parecidos")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.Regulacao, ModuloPermissao.RegulacaoTriagem,
        ModuloPermissao.Sisreg)]
    [ProducesResponseType<IReadOnlyList<MedicoLocalParecidoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MedicoLocalParecidoDto>> Parecidos(
        [FromQuery] SistemaRegulacao sistema, [FromQuery] string? nome, [FromQuery] string? cpf,
        CancellationToken cancellationToken) =>
        medicos.ParecidosAsync(sistema, nome ?? string.Empty, cpf, cancellationToken);

    /// <summary>Inclui o médico (ou devolve o que já existe com o mesmo CPF ou nome).</summary>
    [HttpPost]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.Regulacao, ModuloPermissao.RegulacaoTriagem,
        ModuloPermissao.Sisreg)]
    [ProducesResponseType<MedicoLocalDto>(StatusCodes.Status200OK)]
    public Task<MedicoLocalDto> Criar([FromBody] SalvarMedicoLocalRequest req, CancellationToken cancellationToken) =>
        medicos.CriarAsync(req, cancellationToken);

    [HttpPut("{id:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.RegulacaoTriagem, ModuloPermissao.Sisreg)]
    [ProducesResponseType<MedicoLocalDto>(StatusCodes.Status200OK)]
    public Task<MedicoLocalDto> Atualizar(
        Guid id, [FromBody] SalvarMedicoLocalRequest req, CancellationToken cancellationToken) =>
        medicos.AtualizarAsync(id, req, cancellationToken);

    /// <summary>Junta o cadastro <paramref name="origemId"/> neste — as grafias e os pedidos vêm junto.</summary>
    [HttpPost("{id:guid}/juntar/{origemId:guid}")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.RegulacaoTriagem, ModuloPermissao.Sisreg)]
    [ProducesResponseType<MedicoLocalDto>(StatusCodes.Status200OK)]
    public Task<MedicoLocalDto> Juntar(Guid id, Guid origemId, CancellationToken cancellationToken) =>
        medicos.JuntarAsync(id, origemId, cancellationToken);

    [HttpGet("possiveis-repetidos")]
    [RequerQualquerPermissao(AcoesPermissao.Consulta, ModuloPermissao.RegulacaoTriagem, ModuloPermissao.Sisreg)]
    [ProducesResponseType<IReadOnlyList<PossivelRepetidoDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PossivelRepetidoDto>> PossiveisRepetidos(
        [FromQuery] SistemaRegulacao sistema, CancellationToken cancellationToken) =>
        medicos.PossiveisRepetidosAsync(sistema, cancellationToken);

    /// <summary>Lê as fichas já importadas e acrescenta quem faltar — sem nenhuma requisição ao SISREG.</summary>
    [HttpPost("atualizar-das-fichas")]
    [RequerQualquerPermissao(AcoesPermissao.Edicao, ModuloPermissao.RegulacaoTriagem, ModuloPermissao.Sisreg)]
    [ProducesResponseType<AtualizacaoMedicosLocaisDto>(StatusCodes.Status200OK)]
    public Task<AtualizacaoMedicosLocaisDto> AtualizarDasFichas(
        [FromQuery] SistemaRegulacao sistema, CancellationToken cancellationToken) =>
        medicos.AtualizarDasFichasAsync(sistema, cancellationToken);
}
