using Oracle.ManagedDataAccess.Client;
using SMSMarica.Core.Inteligencia.Validacao;

namespace SMSMarica.Core.Integracoes.Pep.Leitura;

/// <summary>
/// Leitor Oracle de canal único e persistente para importação: abre UMA conexão, marca a
/// transação como <c>READ ONLY</c> uma vez e reaproveita a mesma conexão para todas as
/// queries do run (sem reautenticar por query — o oposto do <c>sqlplus</c> subprocess).
/// Read-only em camadas: conta só-leitura + <c>SET TRANSACTION READ ONLY</c> +
/// <see cref="SqlReadOnlyGuard"/>. Lê colunas direto do <see cref="OracleDataReader"/>,
/// sem <c>JSON_OBJECT</c> (evita o ORA-40474 do charset legado).
/// </summary>
public sealed class LeitorOracleHis : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly int _timeout;
    private OracleConnection? _con;

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
            ConnectionTimeout = 30,
        }.ConnectionString;
    }

    public async Task AbrirAsync(CancellationToken ct)
    {
        _con = new OracleConnection(_connectionString);
        await _con.OpenAsync(ct);

        await using var cmd = _con.CreateCommand();
        cmd.CommandText = "SET TRANSACTION READ ONLY";
        cmd.CommandTimeout = _timeout;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Executa um SELECT (validado read-only) e projeta cada linha via <paramref name="map"/>.</summary>
    public async Task<List<T>> LerAsync<T>(string sql, Func<OracleDataReader, T> map, CancellationToken ct)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);
        if (_con is null) throw new InvalidOperationException("LeitorOracleHis: conexão não aberta (chame AbrirAsync).");

        await using var cmd = _con.CreateCommand();
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

    public async ValueTask DisposeAsync()
    {
        if (_con is not null)
        {
            await _con.DisposeAsync();
            _con = null;
        }
    }
}
