using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.AgendaPontual;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Motor diário que varre a agenda do SISREG da unidade e cria as solicitações.
///
/// <para><b>Todos os endpoints exigem UMA unidade selecionada</b> (header <c>X-Unidade-Id</c>):
/// a credencial do SISREG é de um operador que enxerga uma unidade só.</para>
///
/// <para><b>Somente leitura no SISREG.</b> A varredura usa <c>etapa=ListaConsulta</c>; confirmar
/// presença ou registrar falta escreveria na agenda de verdade e não é feito aqui.</para>
/// </summary>
[ApiController]
[Route("sisreg/varredura")]
public sealed class SisregVarreduraController(
    IVarreduraAgendaService varredura,
    IImportacaoAgendaPontualService importacaoPontual) : ControllerBase
{
    private readonly IVarreduraAgendaService _varredura = varredura;
    private readonly IImportacaoAgendaPontualService _importacaoPontual = importacaoPontual;

    /// <summary>Agenda da unidade + o custo estimado da próxima varredura.</summary>
    [HttpGet("agenda")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<VarreduraAgendaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<VarreduraAgendaDto> ObterAgenda(CancellationToken cancellationToken) =>
        await _varredura.ObterAgendaAsync(cancellationToken);

    /// <summary>
    /// Liga/desliga o sincronismo diário e ajusta hora e janela de dias. Recusa hora fora da
    /// janela permitida — o SISREG mantém uma sessão por operador, e varrer no expediente derruba
    /// o atendente da unidade.
    /// </summary>
    [HttpPut("agenda")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraAgendaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<VarreduraAgendaDto> SalvarAgenda(
        [FromBody] SalvarVarreduraAgendaRequest request, CancellationToken cancellationToken) =>
        await _varredura.SalvarAgendaAsync(request, cancellationToken);

    /// <summary>
    /// Liga/desliga a importação do PASSADO desta unidade — o motor anda para trás em fatias de 31
    /// dias, uma por vez, e para sozinho depois de seis meses seguidos sem nenhum registro.
    ///
    /// <para>É trabalho de fundo: cede lugar a qualquer outro motor do SISREG e só toca quando há
    /// folga de orçamento. Não avisa paciente — importar agenda de meses atrás não pode disparar
    /// WhatsApp sobre consulta que já aconteceu.</para>
    /// </summary>
    [HttpPut("historico")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraAgendaDto>(StatusCodes.Status200OK)]
    public async Task<VarreduraAgendaDto> AlternarHistorico(
        [FromBody] AlternarHistoricoRequest request, CancellationToken cancellationToken) =>
        await _varredura.AlternarHistoricoAsync(request, cancellationToken);

    /// <summary>
    /// Avança UMA fatia do passado agora, por comando do operador.
    ///
    /// <para>Não passa pela chave-mestra: ela pausa a <b>agenda automática</b>, não o que uma
    /// pessoa mandou fazer. Quando não dá para rodar (outro motor na sessão, janela de bloqueio do
    /// SISREG, orçamento), responde com o motivo — nunca com silêncio.</para>
    /// </summary>
    [HttpPost("historico/avancar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraAceitaDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AvancarHistorico(CancellationToken cancellationToken)
    {
        var id = await _varredura.AvancarHistoricoAgoraAsync(cancellationToken);
        return Accepted(new VarreduraAceitaDto(
            id,
            "Fatia do passado iniciada. O progresso aparece em \"Coberto desde\"."));
    }

    /// <summary>Dispara a varredura agora. 202: roda no servidor, fechar a aba não interrompe.</summary>
    [HttpPost("executar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraAceitaDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Executar(CancellationToken cancellationToken)
    {
        var aceita = await _varredura.IniciarAsync(cancellationToken);
        return Accepted(aceita);
    }

    /// <summary>
    /// Dispara uma varredura MANUAL por período específico (inclusive datas passadas). 202: roda no
    /// servidor. Não avisa o paciente por WhatsApp — é backfill. Recusa fora da janela de entrada e
    /// período maior que 31 dias.
    /// </summary>
    [HttpPost("executar-periodo")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<VarreduraAceitaDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecutarPeriodo(
        [FromBody] IniciarVarreduraPeriodoRequest request, CancellationToken cancellationToken)
    {
        var aceita = await _varredura.IniciarPeriodoAsync(request, cancellationToken);
        return Accepted(aceita);
    }

    /// <summary>
    /// Importa PONTUALMENTE a agenda de UM profissional × procedimento no período informado,
    /// consultando o <c>cons_agendas</c> direto (não tem a trava de horário do <c>expo</c>). É o
    /// botão "Importar" da tela de mapeamento, para quando não dá para esperar a varredura noturna.
    /// Síncrono: devolve o resumo do que entrou. Só leitura no SISREG.
    /// </summary>
    [HttpPost("importar-procedimento")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType<ImportacaoAgendaPontualResultado>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ImportacaoAgendaPontualResultado> ImportarProcedimento(
        [FromBody] ImportarAgendaPontualRequest request, CancellationToken cancellationToken) =>
        await _importacaoPontual.ImportarAsync(request, cancellationToken);

    /// <summary>Detalhe por profissional × procedimento de uma execução (o modal do histórico).</summary>
    [HttpGet("execucoes/{id:guid}/itens")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VarreduraExecucaoItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<VarreduraExecucaoItemDto>> Itens(
        Guid id, CancellationToken cancellationToken) =>
        await _varredura.ListarItensAsync(id, cancellationToken);

    /// <summary>Progresso da varredura em curso, ou 204 se não houver nenhuma.</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<StatusVarreduraVivo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await _varredura.ObterStatusAsync(cancellationToken);
        return status is null ? NoContent() : Ok(status);
    }

    /// <summary>Para a varredura em curso. O que já entrou permanece — a importação é por página.</summary>
    [HttpPost("cancelar")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Cancelar() => Ok(new { cancelada = _varredura.Cancelar() });

    /// <summary>Varreduras recentes desta unidade.</summary>
    [HttpGet("execucoes")]
    [RequerPermissao(ModuloPermissao.SisregMapeamento, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<VarreduraExecucaoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<VarreduraExecucaoDto>> Execucoes(
        [FromQuery] int limite, CancellationToken cancellationToken) =>
        await _varredura.ListarExecucoesAsync(limite <= 0 ? 20 : limite, cancellationToken);
}
