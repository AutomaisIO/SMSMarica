using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb;

namespace SMSMais.Core.Sisreg.Sessao;

/// <summary>Estado da sessão do operador no SISREG, para a tela decidir se pede a senha.</summary>
public sealed record SisregSessaoOperadorInfo(
    bool Autenticado, string? UsuarioSisreg, DateTime? AutenticadaEm, DateTime? ExpiraEm);

/// <summary>
/// A sessão de ESCRITA no SISREG, uma por sessão de operador do SMSMais — em MEMÓRIA.
///
/// <para><b>Por que não dá para usar a credencial do banco.</b> Aquela é de <b>sincronismo</b>:
/// serve para a varredura ler a agenda. O SISREG carimba cada cancelamento com o login de quem o
/// fez — é a coluna "Operador" da tela de marcações canceladas, que a própria SMS usa para saber
/// quem desmarcou. Cancelar com a credencial do robô faria todo cancelamento do município sair no
/// nome da mesma pessoa, e a trilha do Ministério passaria a mentir sobre a autoria.</para>
///
/// <para><b>Por que memória e não banco.</b> Mesma régua do SER: a senha do SISREG de cada
/// operador vive junto da sessão dele no SMSMais e some quando ela some. Guardar em banco criaria
/// um cofre de credenciais pessoais do Ministério — risco que a funcionalidade não paga.
/// Consequência aceita: reiniciar a API derruba as sessões e cada operador reautentica.</para>
///
/// <para><b>A chave é a SESSÃO (<c>jti</c> do token), não o usuário</b>, senão quem deslogasse e
/// voltasse herdaria a credencial da sessão anterior — "sair" deixaria de significar sair.</para>
///
/// <para><b>⚠️ Sessão única do SISREG.</b> Ao contrário do SER, o SISREG admite <b>um login por
/// operador</b>: cada novo login derruba o anterior <i>daquele mesmo login</i>. Operadores
/// diferentes convivem sem problema — é o caso normal. Mas se alguém usar aqui o MESMO login que
/// o sincronismo usa, os dois vão se derrubar o dia inteiro, e o sintoma é traiçoeiro: as páginas
/// continuam abrindo e os dados é que vêm vazios.</para>
/// </summary>
public interface ISisregSessaoOperadorStore
{
    /// <summary>Valida a credencial CONTRA O SISREG na hora e guarda a sessão. Credencial errada
    /// não entra no store.</summary>
    Task<SisregSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSisreg, string senha,
        CancellationToken cancellationToken);

    /// <summary>Estado atual, para a tela saber se abre o modal de senha.</summary>
    SisregSessaoOperadorInfo Estado(string sessaoId);

    /// <summary>A sessão de escrita do operador, ou recusa nomeada para a tela pedir a senha.</summary>
    ISisregWebSessao Exigir(string sessaoId);

    /// <summary>Chamado no logout do SMSMais: sair de lá é sair daqui.</summary>
    void Encerrar(string sessaoId);
}

public sealed class SisregSessaoOperadorStore : ISisregSessaoOperadorStore, IDisposable
{
    /// <summary>Recusa nomeada — o front a traduz em modal de senha, não em erro vermelho.</summary>
    public const string CodigoSemSessao = "sisreg.sessao_operador_ausente";

    private readonly ILogger<SisregSessaoOperadorStore> _logger;

    /// <summary>Como nasce uma sessão amarrada a (usuário, senha). Só os testes trocam.</summary>
    private readonly Func<string, string, ISisregWebSessao> _novaSessao;

    public SisregSessaoOperadorStore(
        IServiceScopeFactory scopeFactory,
        SisregOrcamentoRequisicoes orcamento,
        ILoggerFactory loggerFactory,
        ILogger<SisregSessaoOperadorStore> logger)
        : this(logger, (usuario, senha) =>
        {
            var sessao = new SisregWebSessao(
                scopeFactory, orcamento, loggerFactory.CreateLogger<SisregWebSessao>());
            sessao.UsarCredencialDoOperador(usuario, senha);
            return sessao;
        })
    {
    }

    internal SisregSessaoOperadorStore(
        ILogger<SisregSessaoOperadorStore> logger, Func<string, string, ISisregWebSessao> novaSessao)
    {
        _logger = logger;
        _novaSessao = novaSessao;
    }

    /// <summary>
    /// Validade por INATIVIDADE. Oito horas cobrem um plantão sem pedir a senha de novo; errar
    /// para menos custa um modal a mais, para mais é credencial viva de quem já foi embora.
    /// </summary>
    private static readonly TimeSpan Validade = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, Entrada> _sessoes = new(StringComparer.Ordinal);

    public async Task<SisregSessaoOperadorInfo> AutenticarAsync(
        string sessaoId, Guid usuarioId, string usuarioSisreg, string senha,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuarioSisreg) || string.IsNullOrWhiteSpace(senha))
            throw new ValidacaoException(
                "sisreg.credencial_operador_incompleta", "Informe o usuário e a senha do SISREG.");

        var sessao = _novaSessao(usuarioSisreg.Trim(), senha);

        try
        {
            // VALIDA AGORA, não no primeiro cancelamento. Guardar credencial sem provar que ela
            // abre o SISREG empurraria a falha para o meio de um cancelamento — e aí o operador
            // não saberia se o que falhou foi a senha ou o cancelamento.
            await sessao.PostFormAsync(
                "/cgi-bin/cons_verificar",
                new Dictionary<string, string> { ["etapa"] = "" },
                cancellationToken);
        }
        catch
        {
            Descartar(sessao);
            throw;
        }

        var agora = DateTime.UtcNow;
        var entrada = new Entrada(sessao, usuarioSisreg.Trim(), agora) { UltimoUsoEm = agora };

        // Trocar de credencial descarta a anterior: duas sessões vivas do mesmo operador no
        // SISREG se derrubam, e qual "vence" é imprevisível.
        if (_sessoes.TryRemove(sessaoId, out var anterior)) Descartar(anterior.Sessao);
        _sessoes[sessaoId] = entrada;

        _logger.LogInformation(
            "SISREG: operador {Usuario} autenticado para escrita como {UsuarioSisreg}.",
            usuarioId, entrada.UsuarioSisreg);

        return Info(entrada);
    }

    public SisregSessaoOperadorInfo Estado(string sessaoId) =>
        Viva(sessaoId) is { } e ? Info(e) : new SisregSessaoOperadorInfo(false, null, null, null);

    public ISisregWebSessao Exigir(string sessaoId)
    {
        var entrada = Viva(sessaoId)
            ?? throw new ValidacaoException(
                CodigoSemSessao,
                "Para cancelar no SISREG é preciso entrar com o SEU usuário do SISREG. "
                + "A credencial cadastrada no sistema é de sincronismo e não assina ações.");

        entrada.UltimoUsoEm = DateTime.UtcNow;
        return entrada.Sessao;
    }

    public void Encerrar(string sessaoId)
    {
        if (_sessoes.TryRemove(sessaoId, out var e)) Descartar(e.Sessao);
    }

    /// <summary>Fecha o cliente HTTP da sessão — é ele que segura o cookie jar do SISREG.</summary>
    private static void Descartar(ISisregWebSessao sessao) => (sessao as IDisposable)?.Dispose();

    private Entrada? Viva(string sessaoId)
    {
        if (string.IsNullOrEmpty(sessaoId)) return null;
        if (!_sessoes.TryGetValue(sessaoId, out var e)) return null;

        // Rede de segurança para sessão que ninguém encerrou (token expirado, aba fechada): sem
        // isto, credencial de quem foi embora ficaria viva até o restart da API.
        if (DateTime.UtcNow - e.UltimoUsoEm > Validade)
        {
            Encerrar(sessaoId);
            return null;
        }

        return e;
    }

    private static SisregSessaoOperadorInfo Info(Entrada e) =>
        new(true, e.UsuarioSisreg, e.AutenticadaEm, e.UltimoUsoEm + Validade);

    public void Dispose()
    {
        foreach (var e in _sessoes.Values) Descartar(e.Sessao);
        _sessoes.Clear();
    }

    private sealed record Entrada(ISisregWebSessao Sessao, string UsuarioSisreg, DateTime AutenticadaEm)
    {
        public DateTime UltimoUsoEm { get; set; }
    }
}
