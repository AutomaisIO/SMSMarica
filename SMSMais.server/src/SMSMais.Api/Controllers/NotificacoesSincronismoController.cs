using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Notificacoes.Sincronismo;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Telefones que recebem aviso por WhatsApp quando um sincronismo falha ou precisa de gente
/// (CAPTCHA, credencial derrubada, unidade com erro, rodada interrompida).
///
/// <para>Os motores do SISREG, SER e SERNIT rodam de madrugada e sozinhos: sem isto, uma parada
/// só é descoberta quando alguém abre a tela — o que na prática significa descobrir depois que o
/// paciente já perdeu a consulta.</para>
/// </summary>
[ApiController]
[Route("integracoes/{provedor}/notificacoes")]
public sealed class NotificacoesSincronismoController(INotificadorSincronismo notificador) : ControllerBase
{
    /// <summary>Provedores que têm motor de sincronismo — evita criar configuração órfã por typo.</summary>
    private static readonly string[] Suportados = ["sisreg", "ser", "sernit"];

    /// <summary>Telefones cadastrados para receber os avisos deste provedor.</summary>
    [HttpGet]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IReadOnlyList<string>> Listar(string provedor, CancellationToken cancellationToken) =>
        await notificador.ListarTelefonesAsync(Validar(provedor), cancellationToken);

    /// <summary>Define os telefones. Lista vazia desliga o aviso deste provedor.</summary>
    [HttpPut]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IReadOnlyList<string>> Salvar(
        string provedor, [FromBody] SalvarTelefonesNotificacaoRequest request,
        CancellationToken cancellationToken) =>
        await notificador.SalvarTelefonesAsync(Validar(provedor), request.Telefones, cancellationToken);

    /// <summary>
    /// Manda uma mensagem de teste agora, para conferir se o aviso chega de fato. Vale a pena: se
    /// a janela de 24h do WhatsApp estiver fechada, o caminho de reabertura por template é
    /// exercitado aqui, e não na madrugada em que algo quebrou.
    /// </summary>
    [HttpPost("testar")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Testar(string provedor, CancellationToken cancellationToken)
    {
        var chave = Validar(provedor);
        var telefones = await notificador.ListarTelefonesAsync(chave, cancellationToken);
        if (telefones.Count == 0)
        {
            throw new ValidacaoException(
                "integracao.sem_telefone",
                "Nenhum telefone cadastrado para receber os avisos deste sincronismo.");
        }

        await notificador.NotificarAsync(
            chave, "Teste de aviso",
            "Se você recebeu esta mensagem, os avisos de falha de sincronismo estão chegando.",
            ct: cancellationToken);

        return Ok(new { enviados = telefones.Count });
    }

    private static string Validar(string provedor)
    {
        var chave = (provedor ?? string.Empty).Trim().ToLowerInvariant();
        if (!Suportados.Contains(chave))
        {
            throw new ValidacaoException(
                "integracao.provedor_invalido",
                $"Integração \"{provedor}\" não tem motor de sincronismo. Use: {string.Join(", ", Suportados)}.");
        }

        return chave;
    }
}

/// <summary>Telefones com DDD (10 a 13 dígitos, com ou sem código do país).</summary>
public sealed record SalvarTelefonesNotificacaoRequest(IReadOnlyList<string> Telefones);
