using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.PesquisasSatisfacao;
using SMSMarica.Core.PesquisasSatisfacao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Envio da pesquisa de satisfação pela retaguarda — o botão no histórico do paciente.
///
/// <para>Módulo de permissão próprio (<see cref="ModuloPermissao.PesquisaSatisfacao"/>): quem
/// consulta o histórico não deveria, por isso, poder disparar WhatsApp para o cidadão. Enquanto
/// o envio for manual, é aqui que se controla quem valida o fluxo.</para>
///
/// <para>Idempotente por atendimento: reenviar não gera segunda pesquisa (nem segunda nota).
/// Recusa fora da janela de 15 dias e depois de respondida — convite que já nasce vencido só
/// gasta a paciência de quem recebe.</para>
/// </summary>
[ApiController]
[Route("pesquisas-satisfacao")]
public sealed class PesquisasSatisfacaoController(IPesquisasSatisfacaoService pesquisas) : ControllerBase
{
    /// <summary>Dispara o convite por WhatsApp para o atendimento informado.</summary>
    [HttpPost("enviar")]
    [RequerPermissao(ModuloPermissao.PesquisaSatisfacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<EnvioPesquisaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<EnvioPesquisaDto> Enviar(
        [FromBody] EnviarPesquisaRequest request, CancellationToken cancellationToken) =>
        pesquisas.EnviarAsync(request.PacienteId, request.EncounterId, cancellationToken);

    /// <summary>Configuração da pesquisa de uma unidade — a aba na tela da unidade.</summary>
    [HttpGet("unidades/{unidadeId:guid}")]
    [RequerPermissao(ModuloPermissao.PesquisaSatisfacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<PesquisaConfigDto>(StatusCodes.Status200OK)]
    public Task<PesquisaConfigDto> ObterConfig(Guid unidadeId, CancellationToken cancellationToken) =>
        pesquisas.ObterConfigAsync(unidadeId, cancellationToken);

    /// <summary>Salva a configuração. Recusa ligar o envio sem link de resposta.</summary>
    [HttpPut("unidades/{unidadeId:guid}")]
    [RequerPermissao(ModuloPermissao.PesquisaSatisfacao, AcoesPermissao.Edicao)]
    [ProducesResponseType<PesquisaConfigDto>(StatusCodes.Status200OK)]
    public Task<PesquisaConfigDto> SalvarConfig(
        Guid unidadeId, [FromBody] SalvarPesquisaConfigRequest request, CancellationToken cancellationToken) =>
        pesquisas.SalvarConfigAsync(unidadeId, request, cancellationToken);

    /// <summary>
    /// Painel da unidade. <b>"Clicadas" não é "respondidas"</b> — a resposta é da AvanteSocial e
    /// nunca chega aqui.
    /// </summary>
    [HttpGet("unidades/{unidadeId:guid}/painel")]
    [RequerPermissao(ModuloPermissao.PesquisaSatisfacao, AcoesPermissao.Consulta)]
    [ProducesResponseType<PesquisaPainelDto>(StatusCodes.Status200OK)]
    public Task<PesquisaPainelDto> ObterPainel(
        Guid unidadeId, [FromQuery] int dias, CancellationToken cancellationToken) =>
        pesquisas.ObterPainelAsync(unidadeId, dias <= 0 ? 30 : dias, cancellationToken);
}

/// <summary>Atendimento a avaliar. O paciente vem junto porque é ele quem recebe a mensagem.</summary>
public sealed record EnviarPesquisaRequest(Guid PacienteId, Guid EncounterId);
