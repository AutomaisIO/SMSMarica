using Oracle.ManagedDataAccess.Client;
using SMSMarica.Core.Inteligencia.Validacao;

namespace SMSMarica.Core.Inteligencia.Fontes;

/// <summary>
/// Fonte de dados Oracle (Salux / outros HIS Oracle). Execução estritamente read-only:
/// a sessão abre como <c>READ ONLY</c>, o SQL passa pelo <see cref="SqlReadOnlyGuard"/>,
/// aplica timeout de comando e capa o número de linhas devolvidas.
/// </summary>
public sealed class SaluxOracleFonte : IFonteDados
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSegundos;
    private readonly int _maxLinhas;

    /// <param name="host">Host/IP do listener Oracle.</param>
    /// <param name="porta">Porta do listener (default 1521).</param>
    /// <param name="servico">Service name / SID.</param>
    /// <param name="usuario">Conta read-only (ex.: salux_obs).</param>
    /// <param name="senha">Senha já decifrada.</param>
    /// <param name="commandTimeoutSegundos">Timeout de execução por comando.</param>
    /// <param name="maxLinhas">Cap de linhas devolvidas.</param>
    public SaluxOracleFonte(
        string host,
        int porta,
        string servico,
        string usuario,
        string senha,
        int commandTimeoutSegundos = 30,
        int maxLinhas = 1000)
    {
        _commandTimeoutSegundos = commandTimeoutSegundos;
        _maxLinhas = maxLinhas;

        var dataSource =
            $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={porta}))" +
            $"(CONNECT_DATA=(SERVICE_NAME={servico})))";

        var builder = new OracleConnectionStringBuilder
        {
            DataSource = dataSource,
            UserID = usuario,
            Password = senha,
            ConnectionTimeout = 15,
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(string sql, CancellationToken cancellationToken = default)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);

        try
        {
            await using var conexao = new OracleConnection(_connectionString);
            await conexao.OpenAsync(cancellationToken);

            // Sessão read-only: qualquer tentativa de escrita falha no banco também.
            await using (var roCmd = conexao.CreateCommand())
            {
                roCmd.CommandText = "SET TRANSACTION READ ONLY";
                roCmd.CommandTimeout = _commandTimeoutSegundos;
                await roCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var cmd = conexao.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _commandTimeoutSegundos;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var colunas = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                colunas[i] = reader.GetName(i);
            }

            var linhas = new List<IReadOnlyList<object?>>();
            while (linhas.Count < _maxLinhas && await reader.ReadAsync(cancellationToken))
            {
                var linha = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    linha[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                linhas.Add(linha);
            }

            return new ResultadoConsulta(true, colunas, linhas);
        }
        catch (OracleException ex)
        {
            return ResultadoConsulta.ComErro(ex.Message);
        }
    }

    public async Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default)
    {
        await using var conexao = new OracleConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);

        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM dual";
        cmd.CommandTimeout = _commandTimeoutSegundos;

        var resultado = await cmd.ExecuteScalarAsync(cancellationToken);
        return resultado is not null;
    }
}
