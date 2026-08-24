using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Armazenamento;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Credenciais (client_id/client_secret) dos provedores de login OAuth (Microsoft,
/// Facebook, Google). Cifradas, write-only: nunca devolve o valor, só flags de "definido".
/// RBAC <see cref="ModuloPermissao.IntegracoesConfig"/> (staff/painel).
/// </summary>
[ApiController]
[Route("integracoes/credenciais")]
public sealed class IntegracaoCredencialController(IIntegracaoCredencialService service) : ControllerBase
{
    private readonly IIntegracaoCredencialService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<IntegracaoCredencialDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<IntegracaoCredencialDto>> Listar(CancellationToken cancellationToken) =>
        await _service.ListarAsync(cancellationToken);

    [HttpGet("{provedor}")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IntegracaoCredencialDto>(StatusCodes.Status200OK)]
    public async Task<IntegracaoCredencialDto> Obter(string provedor, CancellationToken cancellationToken) =>
        await _service.ObterAsync(provedor, cancellationToken);

    [HttpPut("{provedor}")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(
        string provedor,
        [FromBody] AtualizarIntegracaoCredencialRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(provedor, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{provedor}")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Limpar(string provedor, CancellationToken cancellationToken)
    {
        await _service.LimparAsync(provedor, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Testa a integração do DigitalOcean Spaces de ponta a ponta: conecta, grava, lê e
    /// apaga um objeto pequeno no bucket. Sempre 200 — o sucesso/falha vem no corpo.
    /// </summary>
    [HttpPost("digitalocean_spaces/testar")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType<TesteArmazenamentoSpaces>(StatusCodes.Status200OK)]
    public async Task<TesteArmazenamentoSpaces> TestarSpaces(
        [FromServices] ArmazenamentoSpaces spaces,
        CancellationToken cancellationToken)
        => await spaces.TestarAsync(cancellationToken);
}
