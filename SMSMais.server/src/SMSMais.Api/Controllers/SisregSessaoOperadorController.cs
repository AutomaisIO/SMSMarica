using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Identidade;
using SMSMais.Core.Sisreg.Sessao;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Credencial do operador no SISREG — nunca persistida.</summary>
public sealed record SisregLoginOperadorRequest(string Usuario, string Senha);

/// <summary>
/// A sessão de ESCRITA no SISREG, por operador.
///
/// <para><b>Por que não basta a credencial do sistema.</b> A cadastrada em Integrações é de
/// <b>sincronismo</b>: lê a agenda. O SISREG carimba cada cancelamento com o login de quem o fez
/// — é a coluna "Operador" da tela de marcações canceladas, que a própria SMS usa para saber quem
/// desmarcou. Cancelar com a credencial do robô faria todo cancelamento do município sair no nome
/// da mesma pessoa. Quem cancela assina com o PRÓPRIO login do SISREG.</para>
///
/// <para><b>A senha não é persistida.</b> Vive na memória do processo, amarrada à sessão do
/// operador, e morre com ela (ou com o restart da API). Não existe tabela para ela.</para>
/// </summary>
[ApiController]
[Route("sisreg/sessao")]
public sealed class SisregSessaoOperadorController(
    ISisregSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    /// <summary>A tela pergunta isto antes de oferecer o cancelamento.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregSessaoOperadorInfo>(StatusCodes.Status200OK)]
    public SisregSessaoOperadorInfo Estado() => sessoes.Estado(Sessao());

    /// <summary>Entra no SISREG com a credencial do operador. Valida CONTRA O SISREG na hora.</summary>
    [HttpPost]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Edicao)]
    [ProducesResponseType<SisregSessaoOperadorInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SisregSessaoOperadorInfo> Entrar(
        [FromBody] SisregLoginOperadorRequest corpo, CancellationToken cancellationToken) =>
        sessoes.AutenticarAsync(Sessao(), Operador(), corpo.Usuario, corpo.Senha, cancellationToken);

    /// <summary>Sai do SISREG. O front chama isto no logout — sair daqui é sair de lá.</summary>
    [HttpDelete]
    [RequerPermissao(ModuloPermissao.Confirmacoes, AcoesPermissao.Consulta)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Sair()
    {
        sessoes.Encerrar(Sessao());
        return NoContent();
    }

    /// <summary>A SESSÃO (jti), não o usuário: é o que faz sair-e-entrar começar do zero.</summary>
    private string Sessao() =>
        usuarioAtual.SessaoId
        ?? throw new SMSMais.Core.Common.Excecoes.ValidacaoException(
            "sisreg.sem_operador", "Sessão sem identificação — entre no sistema de novo.");

    private Guid Operador() =>
        usuarioAtual.UsuarioId
        ?? throw new SMSMais.Core.Common.Excecoes.ValidacaoException(
            "sisreg.sem_operador", "Sessão sem identificação — entre no sistema de novo.");
}
