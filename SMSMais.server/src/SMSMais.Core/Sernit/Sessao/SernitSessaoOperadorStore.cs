using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SernitWeb;

namespace SMSMais.Core.Sernit.Sessao;

/// <summary>Estado da sessão do operador no SERNIT, para a tela decidir se pede a senha.</summary>
public sealed record SernitSessaoOperadorInfo(
    bool Autenticado, string? UsuarioSernit, DateTime? AutenticadaEm, DateTime? ExpiraEm);

/// <summary>
/// A sessão de ESCRITA no SERNIT, uma por operador do SMSMais — em MEMÓRIA. Espelho do
/// <c>SerSessaoOperadorStore</c> do SER-RJ.
///
/// <para><b>Por que não a credencial do banco:</b> aquela é de sincronismo (a varredura lê). O
/// SERNIT carimba cada evento com o nome de quem fez; escrever com a credencial de sincronismo
/// faria toda ação do município sair no mesmo nome na trilha do Estado.</para>
///
/// <para><b>Por que memória e não banco:</b> a senha do SERNIT de cada operador vive junto da
/// sessão dele no SMSMais e some quando ela some — não se cria um cofre de credenciais pessoais.</para>
///
/// <para><b>A chave é a SESSÃO (<c>jti</c>), não o usuário:</b> por sessão, cada login começa sem
/// credencial do SERNIT, o logout derruba a dele, e a validade por inatividade recolhe as órfãs.</para>
/// </summary>
public interface ISernitSessaoOperadorStore
{
    Task<SernitSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSernit, string senha,
        CancellationToken cancellationToken);

    SernitSessaoOperadorInfo Estado(string sessaoId);

    ISernitWebSessao Exigir(string sessaoId);

    void Encerrar(string sessaoId);
}

public sealed class SernitSessaoOperadorStore : ISernitSessaoOperadorStore, IDisposable
{
    private readonly ILogger<SernitSessaoOperadorStore> _logger;

    /// <summary>Como nasce uma sessão amarrada a (usuário, senha). Só os testes trocam.</summary>
    private readonly Func<string, string, ISernitWebSessao> _novaSessao;

    public SernitSessaoOperadorStore(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        ILogger<SernitSessaoOperadorStore> logger)
        : this(logger, (usuario, senha) =>
        {
            var sessao = new SernitWebSessao(scopeFactory, loggerFactory.CreateLogger<SernitWebSessao>());
            sessao.UsarCredencialDoOperador(usuario, senha);
            return sessao;
        })
    {
    }

    internal SernitSessaoOperadorStore(
        ILogger<SernitSessaoOperadorStore> logger, Func<string, string, ISernitWebSessao> novaSessao)
    {
        _logger = logger;
        _novaSessao = novaSessao;
    }

    /// <summary>Validade por INATIVIDADE — oito horas cobrem um plantão inteiro.</summary>
    private static readonly TimeSpan Validade = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, Entrada> _sessoes = new(StringComparer.Ordinal);

    public async Task<SernitSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSernit, string senha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioSernit) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "sernit.credencial_operador_incompleta", "Informe o usuário e a senha do SERNIT.");
        }

        var sessao = _novaSessao(usuarioSernit.Trim(), senha);

        try
        {
            // Valida AGORA, não na primeira escrita: guardar credencial sem provar que abre o
            // módulo empurraria a falha para o meio de um FollowUP.
            await sessao.AbrirTelaPesquisaAsync(cancellationToken);
        }
        catch
        {
            Descartar(sessao);
            throw;
        }

        var agora = DateTime.UtcNow;
        var entrada = new Entrada(sessao, usuarioSernit.Trim(), agora) { UltimoUsoEm = agora };

        if (_sessoes.TryRemove(sessaoId, out var anterior)) Descartar(anterior.Sessao);
        _sessoes[sessaoId] = entrada;

        _logger.LogInformation(
            "SERNIT: operador {Usuario} autenticado para escrita como {UsuarioSernit}.",
            usuarioId, entrada.UsuarioSernit);

        return Info(entrada);
    }

    public SernitSessaoOperadorInfo Estado(string sessaoId) =>
        Viva(sessaoId) is { } e ? Info(e) : new SernitSessaoOperadorInfo(false, null, null, null);

    public ISernitWebSessao Exigir(string sessaoId)
    {
        var entrada = Viva(sessaoId)
            ?? throw new ValidacaoException(
                "sernit.sessao_operador_ausente",
                "Para escrever no SERNIT é preciso entrar com o SEU usuário do SERNIT. "
                + "A credencial cadastrada no sistema é de sincronismo e não assina ações.");

        entrada.UltimoUsoEm = DateTime.UtcNow;
        return entrada.Sessao;
    }

    public void Encerrar(string sessaoId)
    {
        if (_sessoes.TryRemove(sessaoId, out var e)) Descartar(e.Sessao);
    }

    private static void Descartar(ISernitWebSessao sessao) => (sessao as IDisposable)?.Dispose();

    private Entrada? Viva(string sessaoId)
    {
        if (string.IsNullOrEmpty(sessaoId)) return null;
        if (!_sessoes.TryGetValue(sessaoId, out var e)) return null;

        if (DateTime.UtcNow - e.UltimoUsoEm > Validade)
        {
            Encerrar(sessaoId);
            return null;
        }

        return e;
    }

    private static SernitSessaoOperadorInfo Info(Entrada e) =>
        new(true, e.UsuarioSernit, e.AutenticadaEm, e.UltimoUsoEm + Validade);

    public void Dispose()
    {
        foreach (var e in _sessoes.Values) Descartar(e.Sessao);
        _sessoes.Clear();
    }

    /// <summary>A senha NÃO fica aqui: quem a guarda é a própria <see cref="SernitWebSessao"/>, em
    /// memória, para relogar quando o SERNIT derrubar a conversa Seam.</summary>
    private sealed record Entrada(ISernitWebSessao Sessao, string UsuarioSernit, DateTime AutenticadaEm)
    {
        public DateTime UltimoUsoEm { get; set; }
    }
}
