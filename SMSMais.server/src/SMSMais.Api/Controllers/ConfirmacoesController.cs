using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Comunicacao.Dtos;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Menu Confirmações: a fila de confirmações de agendamento por WhatsApp (o que está empilhado
/// esperando o horário, retido, enviado), as respostas dos pacientes e as regras de disparo.
/// A resposta do paciente vive só neste sistema — nada daqui é escrito no SISREG.
/// </summary>
[ApiController]
[Route("confirmacoes")]
public sealed class ConfirmacoesController(
    IConfirmacaoConfiguracaoService configuracao,
    IConfirmacoesPainelService painel,
    ILoteConfirmacaoService lote,
    IComunicacaoGestaoService gestao) : ControllerBase
{
    // ---- Fila ----

    [HttpGet("fila/resumo")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<ResumoFilaConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<ResumoFilaConfirmacaoDto> ResumoFila(CancellationToken ct) =>
        await painel.ResumoFilaAsync(ct);

    /// <summary>Lista da fila — só a finalidade confirmação de agendamento.</summary>
    [HttpGet("fila")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaComunicacoesDto>(StatusCodes.Status200OK)]
    public async Task<PaginaComunicacoesDto> Fila(
        [FromQuery] string? status,
        [FromQuery] string? confirmacao,
        [FromQuery] string? texto,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await gestao.ListarAsync(new ComunicacaoFiltroDto(
            status, nameof(FinalidadeComunicacao.ConfirmacaoAgendamento), confirmacao, texto, de, ate, pagina, tamanho), ct);

    [HttpGet("fila/{id:guid}")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<ComunicacaoDetalheDto>(StatusCodes.Status200OK)]
    public async Task<ComunicacaoDetalheDto> ObterDaFila(Guid id, CancellationToken ct) =>
        await gestao.ObterAsync(id, ct);

    /// <summary>Recoloca na fila (respeita a janela de horário: fora dela, sai quando abrir).</summary>
    [HttpPost("fila/{id:guid}/reenviar")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reenviar(Guid id, CancellationToken ct)
    {
        await gestao.ReenviarAsync(id, ct);
        return NoContent();
    }

    // ---- Respostas dos pacientes ----

    /// <summary>Quem confirmou e quem avisou que não vai (com o motivo). <paramref name="resposta"/>:
    /// Confirmada | Cancelada (vazio = as duas). Período sobre a data da resposta.</summary>
    [HttpGet("respostas")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaRespostasConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<PaginaRespostasConfirmacaoDto> Respostas(
        [FromQuery] string? resposta,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? texto,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 50,
        CancellationToken ct = default) =>
        await painel.ListarRespostasAsync(resposta, de, ate, texto, pagina, tamanho, ct);

    // ---- Disparo em lote (estoque que a importação deixou para trás) ----

    /// <summary>Prévia do lote: quantos seriam avisados, por dia, e quantos ficam de fora e por quê.
    /// Não muda nada. <paramref name="de"/>/<paramref name="ate"/> são dias de Brasília, inclusivos
    /// (é como se deixa o dia seguinte de fora e começa na segunda).</summary>
    [HttpGet("lote/previa")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<PreviaLoteConfirmacaoDto>(StatusCodes.Status200OK)]
    public async Task<PreviaLoteConfirmacaoDto> PreviaLote(
        [FromQuery] Guid? unidadeId, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate,
        [FromQuery] bool forcar = false, CancellationToken ct = default) =>
        await lote.PreverAsync(unidadeId, de, ate, forcar, ct);

    /// <param name="Forcar">Ignora as chaves de unidade/procedimento (exige unidade escolhida).</param>
    /// <param name="IgnorarJanela">Este lote sai agora, mesmo fora do horário de envio.</param>
    public sealed record DispararLoteRequest(
        Guid? UnidadeId, DateOnly? De, DateOnly? Ate, bool Forcar = false, bool IgnorarJanela = false);

    /// <summary>Enfileira o lote (o worker envia, respeitando janela e vazão). Idempotente.</summary>
    [HttpPost("lote")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<PreviaLoteConfirmacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<PreviaLoteConfirmacaoDto> DispararLote(
        [FromBody] DispararLoteRequest request, CancellationToken ct) =>
        await lote.DispararAsync(
            request.UnidadeId, request.De, request.Ate, request.Forcar, request.IgnorarJanela, ct);

    // ---- Regras ----

    [HttpGet("configuracao")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<ConfirmacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<ConfirmacaoConfiguracaoDto> ObterConfiguracao(CancellationToken ct) =>
        await configuracao.ObterAsync(ct);

    [HttpPut("configuracao")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<ConfirmacaoConfiguracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ConfirmacaoConfiguracaoDto> SalvarConfiguracao(
        [FromBody] SalvarConfirmacaoConfiguracaoRequest request, CancellationToken ct) =>
        await configuracao.SalvarAsync(request, ct);

    [HttpGet("regras/unidades")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<RegraUnidadeConfirmacaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<RegraUnidadeConfirmacaoDto>> RegrasUnidades(CancellationToken ct) =>
        await painel.ListarRegrasUnidadesAsync(ct);

    public sealed record AlterarRegraUnidadeRequest(bool Enviar);

    [HttpPut("regras/unidades/{unidadeId:guid}")]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AlterarRegraUnidade(
        Guid unidadeId, [FromBody] AlterarRegraUnidadeRequest request, CancellationToken ct)
    {
        await painel.AlterarRegraUnidadeAsync(unidadeId, request.Enviar, ct);
        return NoContent();
    }
}
