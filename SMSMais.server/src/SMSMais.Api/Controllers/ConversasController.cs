using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

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

    /// <summary>
    /// Assume a conversa (claim): o operador vira o responsável e ela sai da fila para a lista
    /// pessoal dele. Conversa de outro responsável → <c>409 conversa.ja_assumida</c>.
    /// </summary>
    [HttpPost("{id:guid}/assumir")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assumir(Guid id, CancellationToken ct)
    {
        await service.AssumirAsync(id, ct);
        return NoContent();
    }

    /// <summary>Devolve a conversa à fila da unidade (limpa o responsável).</summary>
    [HttpPost("{id:guid}/devolver")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Devolver(Guid id, CancellationToken ct)
    {
        await service.DevolverAsync(id, ct);
        return NoContent();
    }

    /// <summary>Devolve a conversa ao robô ("Atendente Virtual"): volta à fila e o robô retoma.</summary>
    [HttpPost("{id:guid}/encaminhar-robo")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EncaminharRobo(Guid id, CancellationToken ct)
    {
        await service.EncaminharParaRoboAsync(id, ct);
        return NoContent();
    }

    /// <summary>Encaminha a conversa para outro atendente (ele vira o responsável).</summary>
    [HttpPost("{id:guid}/encaminhar")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Encaminhar(
        Guid id, [FromBody] EncaminharConversaRequest request, CancellationToken ct)
    {
        await service.EncaminharAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Transfere a conversa para outra unidade (entra na fila de lá, sem responsável).</summary>
    [HttpPost("{id:guid}/transferir")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Transferir(
        Guid id, [FromBody] TransferirConversaRequest request, CancellationToken ct)
    {
        await service.TransferirUnidadeAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Atendentes que podem receber esta conversa por encaminhamento.</summary>
    [HttpGet("{id:guid}/atendentes-elegiveis")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<AtendenteElegivelDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<AtendenteElegivelDto>> AtendentesElegiveis(Guid id, CancellationToken ct) =>
        await service.ListarAtendentesElegiveisAsync(id, ct);

    /// <summary>Unidades da rede que podem receber conversas por transferência.</summary>
    [HttpGet("unidades-destino")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<UnidadeDestinoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<UnidadeDestinoDto>> UnidadesDestino(CancellationToken ct) =>
        await service.ListarUnidadesDestinoAsync(ct);

    /// <summary>Contadores de não-lidas (minhas × fila) para sino/badge sem carregar a lista.</summary>
    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.Conversas, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoConversasDto>(StatusCodes.Status200OK)]
    public async Task<ResumoConversasDto> Resumo(CancellationToken ct) =>
        await service.ObterResumoAsync(ct);
}
