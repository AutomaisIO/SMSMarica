using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Central de Atendimento (chat WhatsApp multi-operador). A visibilidade das listas respeita a(s)
/// unidade(s) do operador; supervisão (<see cref="ModuloPermissao.ConversasSupervisao"/>) vê todas.
/// </summary>
[ApiController]
[Route("conversas")]
public sealed class ConversasController(IConversaService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ConversaListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ConversaListItemDto>> Listar(
        [FromQuery] AbaConversas aba = AbaConversas.Unidade,
        [FromQuery] string? busca = null,
        CancellationToken ct = default) =>
        await service.ListarAsync(aba, busca, ct);

    [HttpGet("templates")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<TemplateWhatsApp>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<TemplateWhatsApp>> Templates(CancellationToken ct) =>
        await service.ListarTemplatesAsync(ct);

    /// <summary>
    /// Acha o paciente para quem abrir a conversa — por nº da solicitação (SISREG), CPF, CNS
    /// ou qualquer parte do nome. Traz o telefone já cadastrado.
    /// </summary>
    [HttpGet("contatos")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ContatoConversaDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ContatoConversaDto>> Contatos(
        [FromQuery] string? termo, CancellationToken ct) =>
        await service.BuscarContatosAsync(termo, ct);

    [HttpGet("{id:guid}")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConversaListItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ConversaListItemDto> ObterPorId(Guid id, CancellationToken ct) =>
        await service.ObterAsync(id, ct);

    /// <summary>
    /// Todos os cadastros que têm o telefone desta conversa — telefone de família aparece em
    /// vários pacientes, e quem atende precisa saber com quem pode estar falando.
    /// </summary>
    [HttpGet("{id:guid}/pacientes")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<PacienteDoTelefoneDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<PacienteDoTelefoneDto>> PacientesDoTelefone(
        Guid id, CancellationToken ct) =>
        await service.ListarPacientesDoTelefoneAsync(id, ct);

    [HttpGet("{id:guid}/mensagens")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<MensagemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<MensagemDto>> Mensagens(Guid id, CancellationToken ct) =>
        await service.ObterMensagensAsync(id, ct);

    [HttpPost]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Inclusao)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Iniciar([FromBody] IniciarConversaRequest request, CancellationToken ct)
    {
        var id = await service.IniciarComTemplateAsync(request, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id }, id);
    }

    [HttpPost("{id:guid}/mensagens")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enviar(Guid id, [FromBody] EnviarMensagemRequest request, CancellationToken ct)
    {
        await service.EnviarTextoAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/lida")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLida(Guid id, CancellationToken ct)
    {
        await service.MarcarLidaAsync(id, ct);
        return NoContent();
    }
}
