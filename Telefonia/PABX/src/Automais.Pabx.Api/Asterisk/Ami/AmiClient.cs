using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace Automais.Pabx.Api.Asterisk.Ami;

/// <summary>Um "pacote" AMI: bloco de linhas chave: valor terminado por linha vazia.</summary>
public sealed class AmiPacote
{
    public Dictionary<string, string> Campos { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Linhas sem "chave: valor" (saída de Command/Response: Follows).</summary>
    public List<string> Saida { get; } = [];

    public string? this[string chave] => Campos.TryGetValue(chave, out var v) ? v : null;
    public bool EhEvento(string nome) => string.Equals(this["Event"], nome, StringComparison.OrdinalIgnoreCase);
}

public interface IAmiClient
{
    /// <summary>Envia uma action e retorna a resposta (pacote Response com o mesmo ActionID).</summary>
    Task<AmiPacote> ExecutarAsync(string action, IDictionary<string, string>? parametros = null, CancellationToken ct = default);

    /// <summary>
    /// Envia uma action cuja resposta vem como lista de eventos (ex.: SIPpeers → PeerEntry*
    /// até o evento terminador). Retorna os eventos intermediários.
    /// </summary>
    Task<IReadOnlyList<AmiPacote>> ExecutarListaAsync(
        string action, string eventoTerminador, IDictionary<string, string>? parametros = null, CancellationToken ct = default);
}

public interface IAmiClientFactory
{
    bool Habilitado { get; }

    /// <summary>Conecta e autentica uma sessão AMI nova. Descartar (await using) ao fim do uso.</summary>
    Task<IAmiConexao> ConectarAsync(CancellationToken ct = default);
}

public interface IAmiConexao : IAmiClient, IAsyncDisposable;

public sealed class AmiClientFactory(IOptions<AsteriskOptions> options) : IAmiClientFactory
{
    private readonly AmiOptions _ami = options.Value.Ami;

    public bool Habilitado => _ami.Enabled;

    public async Task<IAmiConexao> ConectarAsync(CancellationToken ct = default)
    {
        if (!Habilitado)
            throw new InvalidOperationException("AMI está desabilitado na configuração (Asterisk:Ami:Enabled).");

        var conexao = new AmiConexao();
        await conexao.AbrirAsync(_ami, ct);
        return conexao;
    }
}

/// <summary>
/// Cliente AMI mínimo (TCP linha a linha). Uma conexão por operação: conecta, Login,
/// executa e encerra — tráfego baixo não justifica conexão persistente com reconexão.
/// </summary>
internal sealed class AmiConexao : IAmiConexao
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private int _proximoActionId;

    internal async Task AbrirAsync(AmiOptions opcoes, CancellationToken ct)
    {
        _tcp = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await _tcp.ConnectAsync(opcoes.Host, opcoes.Port, timeout.Token);

        var stream = _tcp.GetStream();
        _reader = new StreamReader(stream, Encoding.ASCII);
        _writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

        // Banner: "Asterisk Call Manager/x.y.z"
        await _reader.ReadLineAsync(timeout.Token);

        var login = await ExecutarAsync("Login", new Dictionary<string, string>
        {
            ["Username"] = opcoes.Username,
            ["Secret"] = opcoes.Secret,
        }, timeout.Token);

        if (!string.Equals(login["Response"], "Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Login AMI falhou: {login["Message"] ?? "sem mensagem"}");
    }

    public async Task<AmiPacote> ExecutarAsync(string action, IDictionary<string, string>? parametros = null, CancellationToken ct = default)
    {
        var actionId = await EnviarAsync(action, parametros, ct);

        while (true)
        {
            var pacote = await LerPacoteAsync(ct);
            if (pacote["Response"] is not null && (pacote["ActionID"] is null || pacote["ActionID"] == actionId))
                return pacote;
            // Eventos assíncronos não solicitados são descartados aqui.
        }
    }

    public async Task<IReadOnlyList<AmiPacote>> ExecutarListaAsync(
        string action, string eventoTerminador, IDictionary<string, string>? parametros = null, CancellationToken ct = default)
    {
        var actionId = await EnviarAsync(action, parametros, ct);
        var eventos = new List<AmiPacote>();
        var respostaRecebida = false;

        while (true)
        {
            var pacote = await LerPacoteAsync(ct);

            if (!respostaRecebida && pacote["Response"] is not null && pacote["ActionID"] == actionId)
            {
                if (!string.Equals(pacote["Response"], "Success", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Action {action} falhou: {pacote["Message"] ?? "sem mensagem"}");
                respostaRecebida = true;
                continue;
            }

            if (pacote["ActionID"] != actionId)
                continue;

            if (pacote.EhEvento(eventoTerminador))
                return eventos;

            eventos.Add(pacote);
        }
    }

    private async Task<string> EnviarAsync(string action, IDictionary<string, string>? parametros, CancellationToken ct)
    {
        if (_writer is null)
            throw new InvalidOperationException("Conexão AMI não aberta.");

        var actionId = Interlocked.Increment(ref _proximoActionId).ToString();
        var sb = new StringBuilder();
        sb.Append("Action: ").Append(action).Append("\r\n");
        sb.Append("ActionID: ").Append(actionId).Append("\r\n");
        foreach (var (chave, valor) in parametros ?? new Dictionary<string, string>())
            sb.Append(chave).Append(": ").Append(valor).Append("\r\n");
        sb.Append("\r\n");

        await _writer.WriteAsync(sb.ToString().AsMemory(), ct);
        return actionId;
    }

    private async Task<AmiPacote> LerPacoteAsync(CancellationToken ct)
    {
        if (_reader is null)
            throw new InvalidOperationException("Conexão AMI não aberta.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var pacote = new AmiPacote();
        var temConteudo = false;

        while (true)
        {
            var linha = await _reader.ReadLineAsync(timeout.Token)
                ?? throw new IOException("Conexão AMI encerrada pelo servidor.");

            if (linha.Length == 0)
            {
                if (temConteudo)
                    return pacote;
                continue;
            }

            // "Response: Follows" (saída de Command) encerra com --END COMMAND--.
            if (linha.StartsWith("--END COMMAND--", StringComparison.Ordinal))
                return pacote;

            temConteudo = true;
            var separador = linha.IndexOf(": ", StringComparison.Ordinal);
            if (separador > 0)
                pacote.Campos[linha[..separador]] = linha[(separador + 2)..];
            else
                pacote.Saida.Add(linha);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_writer is not null)
                await _writer.WriteAsync("Action: Logoff\r\n\r\n");
        }
        catch
        {
            // encerrando de qualquer forma
        }

        _reader?.Dispose();
        _writer?.Dispose();
        _tcp?.Dispose();
    }
}
