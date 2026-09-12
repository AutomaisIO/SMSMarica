using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Seção "SISREG" do detalhe de exame e de consulta. O <c>id</c> é o da tela: id do exame
/// (satélite de imagem) ou da própria solicitação — o serviço resolve os dois.
/// </summary>
[ApiController]
[Route("solicitacoes/{id:guid}/sisreg")]
public sealed class SolicitacoesSisregController(IChaveConfirmacaoSisregService chaves) : ControllerBase
{
    /// <summary>Lê no SISREG a chave de confirmação. POST porque não é leitura inocente: gasta o
    /// orçamento anti-robô e fica na auditoria — não pode ser repetida por cache nem por prefetch.</summary>
    [HttpPost("chave")]
    [RequerPermissao(ModuloPermissao.RevelarChaveSisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<ChaveConfirmacaoSisregDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ChaveConfirmacaoSisregDto> RevelarChave(Guid id, CancellationToken cancellationToken)
        => chaves.RevelarAsync(id, cancellationToken);
}
