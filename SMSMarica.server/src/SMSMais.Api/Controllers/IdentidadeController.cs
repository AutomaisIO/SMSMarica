using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

[ApiController]
[Route("usuarios")]
public sealed class IdentidadeController(IIdentidadeService service) : ControllerBase
{
    private readonly IIdentidadeService _service = service;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UsuarioListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UsuarioListItemDto>> Listar(
        [FromQuery] string? busca,
        [FromQuery] Guid? unidadeId,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default) =>
        await _service.ListarAsync(new FiltroUsuariosDto(busca, unidadeId, limite), cancellationToken);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Consulta)]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UsuarioDto> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPorIdAsync(id, cancellationToken);

    /// <summary>Consulta se já existe usuário com este CPF (200 com o usuário, ou 204 No Content).</summary>
    [HttpGet("cpf/{cpf}")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Consulta)]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ObterPorCpf(string cpf, CancellationToken cancellationToken)
    {
        var u = await _service.ObterPorCpfAsync(cpf, cancellationToken);
        return u is null ? NoContent() : Ok(u);
    }

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar(
        [FromBody] CadastrarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _service.CadastrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Exclusao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        await _service.DesativarAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/permissoes")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Consulta)]
    [ProducesResponseType<PermissoesResolvidasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<PermissoesResolvidasDto> ObterPermissoes(Guid id, CancellationToken cancellationToken) =>
        await _service.ObterPermissoesResolvidasAsync(id, cancellationToken);

    [HttpPut("{id:guid}/perfis")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarPerfis(
        Guid id,
        [FromBody] AtualizarPerfisDoUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarPerfisDoUsuarioAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/overrides")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarOverrides(
        Guid id,
        [FromBody] AtualizarOverridesDoUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AtualizarOverridesDoUsuarioAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/senha")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarSenha(
        Guid id,
        [FromBody] AlterarSenhaRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AlterarSenhaAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Gera uma senha aleatória forte para o usuário; sempre força a troca no próximo login.</summary>
    [HttpPost("{id:guid}/senha/gerar")]
    [RequerPermissao(ModuloPermissao.Usuarios, AcoesPermissao.Edicao)]
    [ProducesResponseType<SenhaGeradaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<SenhaGeradaDto> GerarNovaSenha(Guid id, CancellationToken cancellationToken) =>
        await _service.GerarNovaSenhaAsync(id, cancellationToken);
}
