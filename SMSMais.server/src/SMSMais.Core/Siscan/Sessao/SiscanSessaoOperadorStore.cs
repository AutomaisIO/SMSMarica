using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SiscanWeb;

namespace SMSMais.Core.Siscan.Sessao;

/// <summary>Estado da sessão do operador no SISCAN, para a tela decidir se pede a senha.</summary>
public sealed record SiscanSessaoOperadorInfo(
    bool Autenticado, string? UsuarioSiscan, DateTime? AutenticadaEm, DateTime? ExpiraEm);

/// <summary>
/// A sessão do operador no SISCAN, uma por sessão do painel — em MEMÓRIA.
///
/// <para><b>Por que não existe credencial do SISCAN no banco.</b> Diferente do SISREG e do SER,
/// aqui nunca houve credencial de sincronismo — e não vai haver. O SISCAN grava na requisição o
/// <i>responsável</i> e carimba quem operou; uma conta de serviço faria toda requisição do
/// município sair com a mesma autoria, numa base federal de rastreamento de câncer. Quem assina é
/// quem está operando.</para>
///
/// <para><b>Por que memória e não banco.</b> Mesma decisão que o SER (18/08/2026): a senha do
/// operador vive junto da sessão dele no SMSMais e some quando ela some. Guardar em banco criaria
/// um cofre de credenciais pessoais de um sistema do Ministério — risco que a funcionalidade não
/// paga. Consequência aceita: reiniciar a API derruba as sessões e cada um reautentica.</para>
///
/// <para><b>A chave é a SESSÃO (<c>jti</c> do token), não o usuário.</b> Por usuário, quem
/// deslogasse e voltasse herdaria a credencial da sessão anterior — "sair" deixaria de significar
/// sair.</para>
///
/// <para><b>Aviso que ainda não foi medido:</b> no SISREG e no SER, um login novo derruba a sessão
/// anterior daquele operador, inclusive a do navegador dele. No SISCAN isso <b>não foi verificado</b>.
/// Se valer o mesmo, autenticar aqui pode derrubar a pessoa da tela dela — medir antes de soltar
/// para todo mundo.</para>
/// </summary>
public interface ISiscanSessaoOperadorStore
{
    /// <summary>Valida a credencial CONTRA O SISCAN na hora e guarda a sessão. Credencial errada
    /// não entra no store.</summary>
    Task<SiscanSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSiscan, string senha,
        CancellationToken cancellationToken);

    /// <summary>Estado atual, para a tela saber se abre o modal de senha.</summary>
    SiscanSessaoOperadorInfo Estado(string sessaoId);

    /// <summary>A sessão do operador, ou recusa nomeada para a tela pedir a senha.</summary>
    ISiscanWebSessao Exigir(string sessaoId);

    /// <summary>Chamado no logout do SMSMais: sair de lá é sair daqui.</summary>
    void Encerrar(string sessaoId);
}

public sealed class SiscanSessaoOperadorStore : ISiscanSessaoOperadorStore, IDisposable
{
    private readonly ILogger<SiscanSessaoOperadorStore> _logger;

    /// <summary>Como nasce uma sessão amarrada a (usuário, senha). Só os testes trocam.</summary>
    private readonly Func<string, string, ISiscanWebSessao> _novaSessao;

    public SiscanSessaoOperadorStore(
        ILoggerFactory loggerFactory, ILogger<SiscanSessaoOperadorStore> logger)
        : this(logger, (usuario, senha) =>
        {
            var sessao = new SiscanWebSessao(loggerFactory.CreateLogger<SiscanWebSessao>());
            sessao.UsarCredencialDoOperador(usuario, senha);
            return sessao;
        })
    {
    }

    internal SiscanSessaoOperadorStore(
        ILogger<SiscanSessaoOperadorStore> logger, Func<string, string, ISiscanWebSessao> novaSessao)
    {
        _logger = logger;
        _novaSessao = novaSessao;
    }

    /// <summary>
    /// Validade por INATIVIDADE. Oito horas cobrem um plantão sem pedir a senha de novo; errar
    /// para mais é credencial viva em memória de quem já foi embora.
    /// </summary>
    private static readonly TimeSpan Validade = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, Entrada> _sessoes = new(StringComparer.Ordinal);

    public async Task<SiscanSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSiscan, string senha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioSiscan) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "siscan.credencial_operador_incompleta", "Informe o e-mail e a senha do SISCAN.");
        }

        var sessao = _novaSessao(usuarioSiscan.Trim(), senha);

        try
        {
            // VALIDA AGORA, não na primeira requisição. Guardar credencial sem provar que ela
            // entra empurraria a falha para o meio da geração — e aí o operador não sabe se o que
            // falhou foi a senha ou a requisição.
            await sessao.AutenticarAsync(cancellationToken);
        }
        catch
        {
            Descartar(sessao);
            throw;
        }

        var agora = DateTime.UtcNow;
        var entrada = new Entrada(sessao, usuarioSiscan.Trim(), agora) { UltimoUsoEm = agora };

        if (_sessoes.TryRemove(sessaoId, out var anterior)) Descartar(anterior.Sessao);
        _sessoes[sessaoId] = entrada;

        _logger.LogInformation(
            "SISCAN: operador {Usuario} autenticado como {UsuarioSiscan}.",
            usuarioId, entrada.UsuarioSiscan);

        return Info(entrada);
    }

    public SiscanSessaoOperadorInfo Estado(string sessaoId) =>
        Viva(sessaoId) is { } e ? Info(e) : new SiscanSessaoOperadorInfo(false, null, null, null);

    public ISiscanWebSessao Exigir(string sessaoId)
    {
        var entrada = Viva(sessaoId)
            ?? throw new ValidacaoException(
                "siscan.sessao_operador_ausente",
                "Para gerar a requisição é preciso entrar com o SEU usuário do SISCAN. "
                + "Não guardamos essa senha: ela vale enquanto durar a sua sessão no sistema.");

        entrada.UltimoUsoEm = DateTime.UtcNow;
        return entrada.Sessao;
    }

    public void Encerrar(string sessaoId)
    {
        if (_sessoes.TryRemove(sessaoId, out var e)) Descartar(e.Sessao);
    }

    /// <summary>Fecha o cliente HTTP da sessão — é ele que segura o cookie jar do SISCAN.</summary>
    private static void Descartar(ISiscanWebSessao sessao) => (sessao as IDisposable)?.Dispose();

    private Entrada? Viva(string sessaoId)
    {
        if (string.IsNullOrEmpty(sessaoId)) return null;
        if (!_sessoes.TryGetValue(sessaoId, out var e)) return null;

        // Rede de segurança para sessão que ninguém encerrou (token expirado, aba fechada).
        if (DateTime.UtcNow - e.UltimoUsoEm > Validade)
        {
            Encerrar(sessaoId);
            return null;
        }

        return e;
    }

    private static SiscanSessaoOperadorInfo Info(Entrada e) =>
        new(true, e.UsuarioSiscan, e.AutenticadaEm, e.UltimoUsoEm + Validade);

    public void Dispose()
    {
        foreach (var e in _sessoes.Values) Descartar(e.Sessao);
        _sessoes.Clear();
    }

    /// <summary>A senha NÃO fica aqui: quem a guarda é a própria <see cref="SiscanWebSessao"/>,
    /// em memória, para conseguir relogar quando o SISCAN derrubar a sessão.</summary>
    private sealed record Entrada(ISiscanWebSessao Sessao, string UsuarioSiscan, DateTime AutenticadaEm)
    {
        public DateTime UltimoUsoEm { get; set; }
    }
}
