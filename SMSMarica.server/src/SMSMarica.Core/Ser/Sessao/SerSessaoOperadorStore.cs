using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.SerWeb;

namespace SMSMarica.Core.Ser.Sessao;

/// <summary>Estado da sessão do operador no SER, para a tela decidir se pede a senha.</summary>
public sealed record SerSessaoOperadorInfo(
    bool Autenticado, string? UsuarioSer, DateTime? AutenticadaEm, DateTime? ExpiraEm);

/// <summary>
/// A sessão de ESCRITA no SER, uma por operador do SMSMarica — em MEMÓRIA.
///
/// <para><b>Por que não dá para usar a credencial do banco.</b> Aquela é de <b>sincronismo</b>:
/// serve para a varredura ler a fila. O SER carimba cada evento com o nome de quem fez, e foi
/// medido em 18/08/2026 — o FollowUP de teste saiu como <i>"BERNARDO DOS SANTOS LEITE ALMEIDA ·
/// Gestor: GESTOR SMS MARICA"</i>. Escrever com a credencial de sincronismo faria toda ação do
/// município sair no nome da mesma pessoa: a trilha do Estado passaria a mentir sobre a autoria,
/// e é trilha de auditoria de saúde pública.</para>
///
/// <para><b>Por que memória e não banco.</b> Decisão do Bernardo (18/08/2026): a senha do SER de
/// cada operador vive junto da sessão dele no SMSMarica e some quando ela some. Guardar em banco
/// criaria um cofre de credenciais pessoais do Estado — risco que a funcionalidade não paga.
/// Consequência aceita: reiniciar a API derruba as sessões e cada operador reautentica.</para>
///
/// <para><b>A chave é a SESSÃO (<c>jti</c> do token), não o usuário.</b> Por usuário, quem
/// deslogasse e voltasse herdaria a credencial da sessão anterior — "sair" deixaria de significar
/// sair. Por sessão, cada login começa sem credencial do SER, o logout derruba a dele, e um token
/// expirado deixa a entrada órfã, que a validade por inatividade recolhe.</para>
///
/// <para>Múltiplas sessões simultâneas são seguras: medido em 08/08/2026, o SER aceita logins
/// concorrentes (a "sessão única" herdada do SISREG por analogia é falsa — docs/ser.md).</para>
/// </summary>
public interface ISerSessaoOperadorStore
{
    /// <summary>Valida a credencial CONTRA O SER na hora e guarda a sessão. Credencial errada
    /// não entra no store.</summary>
    Task<SerSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSer, string senha,
        CancellationToken cancellationToken);

    /// <summary>Estado atual, para a tela saber se abre o modal de senha.</summary>
    SerSessaoOperadorInfo Estado(string sessaoId);

    /// <summary>A sessão de escrita do operador, ou recusa nomeada para a tela pedir a senha.</summary>
    ISerWebSessao Exigir(string sessaoId);

    /// <summary>Chamado no logout do SMSMarica: sair de lá é sair daqui.</summary>
    void Encerrar(string sessaoId);
}

public sealed class SerSessaoOperadorStore : ISerSessaoOperadorStore, IDisposable
{
    private readonly ILogger<SerSessaoOperadorStore> _logger;

    /// <summary>Como nasce uma sessão amarrada a (usuário, senha). Só os testes trocam.</summary>
    private readonly Func<string, string, ISerWebSessao> _novaSessao;

    public SerSessaoOperadorStore(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        ILogger<SerSessaoOperadorStore> logger)
        : this(logger, (usuario, senha) =>
        {
            var sessao = new SerWebSessao(scopeFactory, loggerFactory.CreateLogger<SerWebSessao>());
            sessao.UsarCredencialDoOperador(usuario, senha);
            return sessao;
        })
    {
    }

    internal SerSessaoOperadorStore(
        ILogger<SerSessaoOperadorStore> logger, Func<string, string, ISerWebSessao> novaSessao)
    {
        _logger = logger;
        _novaSessao = novaSessao;
    }

    /// <summary>
    /// Validade por INATIVIDADE. Oito horas cobre um plantão inteiro sem pedir a senha de novo, e
    /// o custo de errar para menos é só um modal a mais — para mais, é credencial viva em memória
    /// de quem foi embora.
    /// </summary>
    private static readonly TimeSpan Validade = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, Entrada> _sessoes = new(StringComparer.Ordinal);

    public async Task<SerSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSer, string senha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioSer) || string.IsNullOrWhiteSpace(senha))
        {
            throw new ValidacaoException(
                "ser.credencial_operador_incompleta", "Informe o usuário e a senha do SER.");
        }

        var sessao = _novaSessao(usuarioSer.Trim(), senha);

        try
        {
            // VALIDA AGORA, não na primeira escrita. Guardar credencial sem provar que ela abre o
            // módulo Ambulatório empurraria a falha para o meio de um FollowUP — e aí o operador
            // não sabe se o que falhou foi a senha ou o registro.
            await sessao.AbrirTelaPesquisaAsync(cancellationToken);
        }
        catch
        {
            Descartar(sessao);
            throw;
        }

        var agora = DateTime.UtcNow;
        var entrada = new Entrada(sessao, usuarioSer.Trim(), agora) { UltimoUsoEm = agora };

        // Trocar de credencial descarta a anterior: duas sessões vivas do mesmo operador seriam
        // duas conversas Seam concorrentes, e a que "vence" seria imprevisível.
        if (_sessoes.TryRemove(sessaoId, out var anterior)) Descartar(anterior.Sessao);
        _sessoes[sessaoId] = entrada;

        _logger.LogInformation(
            "SER: operador {Usuario} autenticado para escrita como {UsuarioSer}.",
            usuarioId, entrada.UsuarioSer);

        return Info(entrada);
    }

    public SerSessaoOperadorInfo Estado(string sessaoId) =>
        Viva(sessaoId) is { } e ? Info(e) : new SerSessaoOperadorInfo(false, null, null, null);

    public ISerWebSessao Exigir(string sessaoId)
    {
        var entrada = Viva(sessaoId)
            ?? throw new ValidacaoException(
                "ser.sessao_operador_ausente",
                "Para escrever no SER é preciso entrar com o SEU usuário do SER. "
                + "A credencial cadastrada no sistema é de sincronismo e não assina ações.");

        entrada.UltimoUsoEm = DateTime.UtcNow;
        return entrada.Sessao;
    }

    public void Encerrar(string sessaoId)
    {
        if (_sessoes.TryRemove(sessaoId, out var e)) Descartar(e.Sessao);
    }

    /// <summary>Fecha o cliente HTTP da sessão — é ele que segura o cookie jar do SER.</summary>
    private static void Descartar(ISerWebSessao sessao) => (sessao as IDisposable)?.Dispose();

    private Entrada? Viva(string sessaoId)
    {
        if (string.IsNullOrEmpty(sessaoId)) return null;
        if (!_sessoes.TryGetValue(sessaoId, out var e)) return null;

        // Rede de segurança para sessão que ninguém encerrou (token expirado, aba fechada):
        // sem isto, credencial de quem foi embora ficaria viva até o restart da API.
        if (DateTime.UtcNow - e.UltimoUsoEm > Validade)
        {
            Encerrar(sessaoId);
            return null;
        }

        return e;
    }

    private static SerSessaoOperadorInfo Info(Entrada e) =>
        new(true, e.UsuarioSer, e.AutenticadaEm, e.UltimoUsoEm + Validade);

    public void Dispose()
    {
        foreach (var e in _sessoes.Values) Descartar(e.Sessao);
        _sessoes.Clear();
    }

    /// <summary>A senha NÃO fica aqui: quem a guarda é a própria <see cref="SerWebSessao"/>, em
    /// memória, para conseguir relogar quando o SER derrubar a conversa Seam.</summary>
    private sealed record Entrada(ISerWebSessao Sessao, string UsuarioSer, DateTime AutenticadaEm)
    {
        public DateTime UltimoUsoEm { get; set; }
    }
}
