using System.Data;
using Oracle.ManagedDataAccess.Client;
using SMSMarica.Core.Inteligencia.Validacao;

namespace SMSMarica.Core.Integracoes.Pep.Leitura;

/// <summary>
/// Leitor Oracle de canal único e persistente para importação: abre UMA conexão, marca a
/// transação como <c>READ ONLY</c> e reaproveita a mesma conexão para todas as queries do run.
/// Read-only em camadas: conta só-leitura + <c>SET TRANSACTION READ ONLY</c> +
/// <see cref="SqlReadOnlyGuard"/>. Lê colunas direto do <see cref="OracleDataReader"/> (sem
/// <c>JSON_OBJECT</c>).
///
/// Resiliência ao túnel intermitente (WireGuard hub→hospital pisca): a (re)conexão tenta de
/// forma <b>constante e permanente</b> (intervalo fixo, sem backoff) e também reconecta se o
/// link cair no meio de uma leitura. Cada tentativa é reportada para a fase ao vivo.
/// </summary>
public sealed class LeitorOracleHis : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly int _timeout;
    private OracleConnection? _con;

    private const int IntervaloSegundos = 10;     // retry CONSTANTE (sem aumento sucessivo)
    private const int MaxTentativas = 90;         // ~15 min "sempre tentando" (respeita o CancellationToken)

    // Erros transitórios de conexão (túnel/listener piscando) — reconecta e tenta de novo.
    private static readonly int[] CodigosTransitorios =
        [12170, 12541, 12543, 12535, 12537, 12152, 3113, 3114, 50201];

    public LeitorOracleHis(string host, int porta, string servico, string usuario, string senha, int timeoutSegundos)
    {
        _timeout = timeoutSegundos;
        var dataSource =
            $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={porta}))" +
            $"(CONNECT_DATA=(SERVICE_NAME={servico})))";

        _connectionString = new OracleConnectionStringBuilder
        {
            DataSource = dataSource,
            UserID = usuario,
            Password = senha,
            ConnectionTimeout = 8, // connect curto: tentativas cadenciadas, fase ao vivo responsiva
        }.ConnectionString;
    }

    /// <param name="reportar">Callback do número da tentativa — alimenta a fase ao vivo na tela.</param>
    public Task AbrirAsync(CancellationToken ct, Action<int>? reportar = null) => GarantirAbertaAsync(ct, reportar);

    private async Task GarantirAbertaAsync(CancellationToken ct, Action<int>? reportar)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            reportar?.Invoke(tentativa);
            try
            {
                _con = new OracleConnection(_connectionString);
                await _con.OpenAsync(ct);

                await using var cmd = _con.CreateCommand();
                cmd.CommandText = "SET TRANSACTION READ ONLY";
                cmd.CommandTimeout = _timeout;
                await cmd.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (OracleException ex) when (tentativa < MaxTentativas && EhTransitorio(ex))
            {
                await DescartarAsync();
                await Task.Delay(TimeSpan.FromSeconds(IntervaloSegundos), ct);
            }
            catch
            {
                await DescartarAsync();
                throw;
            }
        }
    }

    /// <summary>Executa um SELECT (validado read-only). Reconecta e repete se o túnel cair no meio.</summary>
    public async Task<List<T>> LerAsync<T>(string sql, Func<OracleDataReader, T> map, CancellationToken ct)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                if (_con is null || _con.State != ConnectionState.Open)
                    await GarantirAbertaAsync(ct, null);

                await using var cmd = _con!.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = _timeout;

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                var oracleReader = (OracleDataReader)reader;
                var linhas = new List<T>();
                while (await reader.ReadAsync(ct))
                {
                    linhas.Add(map(oracleReader));
                }
                return linhas;
            }
            catch (OracleException ex) when (tentativa < MaxTentativas && EhTransitorio(ex))
            {
                await DescartarAsync();
                await Task.Delay(TimeSpan.FromSeconds(IntervaloSegundos), ct);
            }
        }
    }

    private static bool EhTransitorio(OracleException ex) =>
        CodigosTransitorios.Contains(ex.Number)
        || ex.Message.Contains("ORA-12170", StringComparison.Ordinal)
        || ex.Message.Contains("ORA-50201", StringComparison.Ordinal);

    private async Task DescartarAsync()
    {
        if (_con is not null)
        {
            try { await _con.DisposeAsync(); } catch { /* ignore */ }
            _con = null;
        }
    }

    public ValueTask DisposeAsync() => new(DescartarAsync());
}
