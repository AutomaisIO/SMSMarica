using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SiscanWeb.Requisicao;
using SMSMais.Core.Integracoes.SiscanWeb.Requisicao.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Gerar, a partir da anamnese, a requisição de mamografia no SISCAN.
///
/// <para>São dois passos de propósito. O <c>preparar</c> percorre o assistente do SISCAN sem
/// gravar e devolve o que será enviado, quem pode assinar e o que falta responder — é o que a
/// tela mostra antes de perguntar "confirma?". O <c>gerar</c> é o único que escreve, e escreve em
/// <b>produção federal</b>.</para>
///
/// <para>Os dois exigem a sessão do SISCAN do próprio operador (<c>/siscan/sessao</c>): a
/// requisição fica carimbada com quem a fez, e credencial de serviço faria a trilha mentir.</para>
/// </summary>
[ApiController]
[Route("siscan/requisicao")]
public sealed class SiscanRequisicaoController(ISiscanRequisicaoService servico) : ControllerBase
{
    /// <summary>O que será enviado, quem pode assinar e o que falta. NÃO grava.</summary>
    [HttpGet("{exameImagemId:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SiscanPreparoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SiscanPreparoDto> Preparar(Guid exameImagemId, CancellationToken cancellationToken) =>
        servico.PrepararAsync(exameImagemId, cancellationToken);

    /// <summary>Cria a requisição no SISCAN e carimba protocolo e nº do exame no nosso pedido.</summary>
    [HttpPost("{exameImagemId:guid}")]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<SiscanRequisicaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<SiscanRequisicaoDto> Gerar(
        Guid exameImagemId, [FromBody] SiscanGerarRequest corpo, CancellationToken cancellationToken) =>
        servico.GerarAsync(exameImagemId, corpo, cancellationToken);
}
