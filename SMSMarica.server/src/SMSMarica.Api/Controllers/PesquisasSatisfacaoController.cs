using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.PesquisasSatisfacao;
using SMSMarica.Core.PesquisasSatisfacao.Dtos;
using SMSMarica.Data.Entities.Enums;

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
}

/// <summary>Atendimento a avaliar. O paciente vem junto porque é ele quem recebe a mensagem.</summary>
public sealed record EnviarPesquisaRequest(Guid PacienteId, Guid EncounterId);
