using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Sisreg;
using SMSMais.Core.Integracoes.Sisreg.Configuracao;
using SMSMais.Core.Integracoes.Sisreg.Dtos;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Notificacoes.AgendamentosExternos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Tela de configuração da integração SISREG: credenciais (cifradas, write-only),
/// UF/município, centrais reguladoras e esquema de autenticação. Ver ADR-0012.
/// </summary>
[ApiController]
[Route("sisreg/configuracao")]
public sealed class SisregConfiguracaoController(
    ISisregConfiguracaoService configuracaoService,
    ISisregConsultaService consultaService,
    IBackfillExecutanteService backfillExecutante) : ControllerBase
{
    private readonly ISisregConfiguracaoService _configuracaoService = configuracaoService;
    private readonly ISisregConsultaService _consultaService = consultaService;
    private readonly IBackfillExecutanteService _backfillExecutante = backfillExecutante;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<SisregConfiguracaoDto> Obter(CancellationToken cancellationToken) =>
        await _configuracaoService.ObterAsync(cancellationToken);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(
        [FromBody] AtualizarSisregConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        await _configuracaoService.AtualizarAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Avisos ao paciente do SISREG (as mesmas chaves de Mensageria → Regras): lembrete X dias antes e aviso de cancelamento (21/10/2026).</summary>
    [HttpGet("avisos-paciente")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<AvisosPacienteRegulacaoDto>(StatusCodes.Status200OK)]
    public Task<AvisosPacienteRegulacaoDto> ObterAvisosPaciente(
        [FromServices] IAvisoRegulacaoConfiguracaoService avisos, CancellationToken cancellationToken) =>
        avisos.ObterAsync(SistemaRegulacao.Sisreg, cancellationToken);

    [HttpPut("avisos-paciente")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<AvisosPacienteRegulacaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<AvisosPacienteRegulacaoDto> SalvarAvisosPaciente(
        [FromBody] SalvarAvisosPacienteRegulacaoRequest corpo,
        [FromServices] IAvisoRegulacaoConfiguracaoService avisos,
        CancellationToken cancellationToken) =>
        avisos.SalvarAsync(SistemaRegulacao.Sisreg, corpo, cancellationToken);

    /// <summary>
    /// Liga/desliga o sincronismo AUTOMÁTICO com o SISREG — varredura diária das unidades e lote de
    /// mapeamento ("sincronizar tudo", inclusive a carga inicial).
    ///
    /// <para>Não é o mesmo que "Desabilitar todas" da tela de sincronismo: aquele grava
    /// <c>Ativo=false</c> nas ~45 agendas e perde quem estava ligado; este só ignora o agendamento,
    /// preservando a programação. Ações manuais (executar agora, importar procedimento) seguem
    /// funcionando — o interruptor é do que roda sem ninguém pedir.</para>
    /// </summary>
    [HttpPut("sincronismo-automatico")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<SincronismoAutomaticoSisregDto>(StatusCodes.Status200OK)]
    public async Task<SincronismoAutomaticoSisregDto> AlternarSincronismoAutomatico(
        [FromBody] AlternarSincronismoAutomaticoRequest request,
        CancellationToken cancellationToken) =>
        await _configuracaoService.AlternarSincronismoAutomaticoAsync(request.Ativo, cancellationToken);

    /// <summary>A fila de guias do SISREG (ADR-0076): ritmo, janela, quanto falta e por que parou
    /// (outro motor rodando, orçamento curto, CAPTCHA).</summary>
    [HttpGet("guias")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SMSMais.Core.GuiasAgendamento.FilaGuiasDto>(StatusCodes.Status200OK)]
    public Task<SMSMais.Core.GuiasAgendamento.FilaGuiasDto> ObterFilaGuias(
        [FromServices] SMSMais.Core.GuiasAgendamento.IFilaGuiasConfiguracaoService guias, CancellationToken cancellationToken) =>
        guias.ResumoAsync(SistemaRegulacao.Sisreg, cancellationToken);

    [HttpPut("guias")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<SMSMais.Core.GuiasAgendamento.FilaGuiasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SMSMais.Core.GuiasAgendamento.FilaGuiasDto> SalvarFilaGuias(
        [FromBody] SMSMais.Core.GuiasAgendamento.SalvarFilaGuiasRequest corpo,
        [FromServices] SMSMais.Core.GuiasAgendamento.IFilaGuiasConfiguracaoService guias,
        CancellationToken cancellationToken) =>
        guias.SalvarAsync(SistemaRegulacao.Sisreg, corpo, cancellationToken);

    /// <summary>
    /// Completa as solicitações já importadas com o profissional EXECUTANTE, relendo a linha crua do
    /// SISREG que ficou guardada. <b>Não fala com o SISREG</b> — é reprocessamento do próprio banco,
    /// então não gasta o orçamento anti-robô nem disputa a sessão do operador.
    ///
    /// <para>Idempotente: só toca em solicitação com o campo vazio. Rodar de novo é seguro.</para>
    /// </summary>
    [HttpPost("backfill-executante")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<BackfillExecutanteResultado>(StatusCodes.Status200OK)]
    public async Task<BackfillExecutanteResultado> BackfillExecutante(CancellationToken cancellationToken) =>
        await _backfillExecutante.ExecutarAsync(cancellationToken);

    /// <summary>Testa as credenciais com uma consulta mínima à fila. Não lança — devolve sucesso/erro.</summary>
    [HttpPost("testar-conexao")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<TestarConexaoSisregResultado>(StatusCodes.Status200OK)]
    public async Task<TestarConexaoSisregResultado> TestarConexao(CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _consultaService.FilaSolicitacoesAmbulatoriaisAsync(1, cancellationToken);
            return new TestarConexaoSisregResultado(true, $"Conexão OK. {resultado.Total} registro(s) na fila.");
        }
        catch (ValidacaoException ex)
        {
            return new TestarConexaoSisregResultado(false, ex.Message);
        }
        catch (ConflitoException ex)
        {
            return new TestarConexaoSisregResultado(false, ex.Message);
        }
    }
}
