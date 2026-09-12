using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.SisregWeb.Fila;
using SMSMais.Core.Integracoes.SisregWeb.Fila.Background;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Leitura da <b>fila de espera</b> do SISREG, pela tela de Configuração do SISREG: situação, o
/// horário da releitura completa diária e o disparo manual.
///
/// <para>Saiu da tela de Ofertas de propósito (12/09/2026): disparar ~88 requisições ao SISREG é
/// decisão de quem cuida da integração, não de quem está regulando. Ofertas continua mostrando a
/// situação da leitura (<c>GET /sisreg/ofertas/fila/status</c>), sem botão.</para>
///
/// <para><b>Nada aqui lê o SISREG na hora.</b> O disparo só enfileira; quem lê é o agendador, uma
/// janela de 31 dias por vez.</para>
/// </summary>
[ApiController]
[Route("sisreg/fila")]
public sealed class SisregFilaController(
    IFilaPendenteSisregService fila,
    FilaPendenteEstadoVivo estado,
    IIntegracaoCredencialService credenciais) : ControllerBase
{
    /// <summary>A fila já foi lida? Está lendo agora? Quantas pessoas há nela?</summary>
    [HttpGet("status")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<FilaCargaStatusDto>(StatusCodes.Status200OK)]
    public async Task<FilaCargaStatusDto> Status(CancellationToken cancellationToken = default) =>
        estado.Snapshot(await fila.ResumoAsync(cancellationToken));

    /// <summary>
    /// Pede a leitura da fila agora. Só enfileira — o agendador lê uma janela por vez, cedendo a vez
    /// aos outros motores.
    /// </summary>
    /// <param name="completa"><c>true</c>: o acervo inteiro desde jan/2023 (~44 janelas, 2
    /// requisições cada). <c>false</c>: só os últimos 31 dias (2 requisições).</param>
    [HttpPost("carregar")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<FilaCargaStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<FilaCargaStatusDto> Carregar(
        [FromQuery] bool completa = false, CancellationToken cancellationToken = default)
    {
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var janelas = completa
            ? JanelasDaFila.Completa(hoje, JanelasDaFila.InicioDoAcervo)
            : JanelasDaFila.Recente(hoje);

        if (!estado.Enfileirar(janelas, completa))
        {
            throw new ConflitoException(
                "fila.em_andamento", "Já há uma leitura da fila em andamento. Acompanhe o progresso.");
        }

        return estado.Snapshot(await fila.ResumoAsync(cancellationToken));
    }

    /// <summary>Releitura completa diária: ligada ou não, e a hora (Brasília).</summary>
    [HttpGet("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<FilaAgendamentoDto>(StatusCodes.Status200OK)]
    public async Task<FilaAgendamentoDto> ObterAgendamento(CancellationToken cancellationToken = default) =>
        await AgendamentoDaFila.ObterAsync(credenciais, cancellationToken);

    [HttpPut("agendamento")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<FilaAgendamentoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<FilaAgendamentoDto> SalvarAgendamento(
        [FromBody] SalvarFilaAgendamentoRequest request, CancellationToken cancellationToken = default) =>
        await AgendamentoDaFila.SalvarAsync(credenciais, request, cancellationToken);
}
