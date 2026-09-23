using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Siscan.Sessao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Credencial do operador no SISCAN — nunca persistida.</summary>
public sealed record SiscanLoginOperadorRequest(string Usuario, string Senha);

/// <summary>
/// A sessão do operador no SISCAN.
///
/// <para><b>Aqui não existe "credencial do sistema".</b> Diferente do SISREG e do SER, o SISCAN
/// nunca teve credencial de sincronismo cadastrada — e não vai ter. A requisição que se cria lá
/// leva um <i>responsável</i> e fica carimbada com quem operou, numa base federal de rastreamento
/// de câncer. Quem gera assina com o próprio login.</para>
///
/// <para><b>A senha não é persistida.</b> Vive na memória do processo, amarrada à sessão do
/// operador no painel, e morre com ela (ou com o restart da API). Não existe tabela para ela.</para>
/// </summary>
[ApiController]
[Route("siscan/sessao")]
public sealed class SiscanSessaoOperadorController(
    ISiscanSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>A tela pergunta isto antes de oferecer "Gerar Requisição SISCAN".</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType<SiscanSessaoOperadorInfo>(StatusCodes.Status200OK)]
    public SiscanSessaoOperadorInfo Estado() => sessoes.Estado(Sessao());

    /// <summary>Entra no SISCAN com a credencial do operador. Valida CONTRA O SISCAN na hora.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Edicao)]
    [ProducesResponseType<SiscanSessaoOperadorInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SiscanSessaoOperadorInfo> Entrar(
        [FromBody] SiscanLoginOperadorRequest corpo, CancellationToken cancellationToken) =>
        sessoes.AutenticarAsync(Sessao(), Operador(), corpo.Usuario, corpo.Senha, cancellationToken);

    /// <summary>Sai do SISCAN. O front chama isto no logout — sair daqui é sair de lá.</summary>
    [HttpDelete]
    [RequerPermissao(ModuloPermissao.SolicitacoesExame, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Sair()
    {
        sessoes.Encerrar(Sessao());
        return NoContent();
    }

    /// <summary>A SESSÃO (jti), não o usuário: é o que faz sair-e-entrar começar do zero.</summary>
    private string Sessao() =>
        usuarioAtual.SessaoId
        ?? throw new ValidacaoException(
            "siscan.sem_operador", "Sessão sem identificação — entre no sistema de novo.");

    private Guid Operador() =>
        usuarioAtual.UsuarioId
        ?? throw new ValidacaoException(
            "siscan.sem_operador", "Sessão sem identificação — entre no sistema de novo.");
}
