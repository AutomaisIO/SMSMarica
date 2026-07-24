using Oracle.ManagedDataAccess.Client;

namespace SMSMarica.Secretario.Api.Oracle;

/// <summary>
/// Fonte de dados Oracle do Salux HIS. Execução estritamente read-only:
/// a sessão abre como <c>READ ONLY</c>, o SQL passa pelo <see cref="SqlReadOnlyGuard"/>,
/// aplica timeout de comando e capa o número de linhas devolvidas.
///
/// Resiliência: o caminho até o Oracle passa por túnel (WireGuard) que pode piscar.
/// Erros transitórios de conexão (ORA-50201/50000/12170…) fazem retry curto com limpeza
/// do pool de conexões mortas — a janela é curta de propósito: o painel roda em cadência
/// fixa e a próxima passada do ciclo já tenta de novo.
///
/// Adaptado de <c>SMSMarica.server/src/SMSMarica.Core/Inteligencia/Fontes/SaluxOracleFonte.cs</c>.
/// </summary>
public sealed class SaluxOracleFonte
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSegundos;
    private readonly int _maxLinhas;

    // Janela CURTA (1 retry): pega um pisca rápido do túnel sem segurar o ciclo por muito tempo.
    private const int MaxTentativas = 2;
    private const int IntervaloSegundos = 2;

    // Erros transitórios de conexão (túnel/pool/listener) — fazem retry. SQL inválido NÃO entra aqui.
    private static readonly int[] CodigosTransitorios =
        [50201, 50000, 12170, 12541, 12543, 12535, 12537, 12152, 3113, 3114];

    public SaluxOracleFonte(
        string host,
        int porta,
        string servico,
        string usuario,
        string senha,
        int commandTimeoutSegundos = 120, // a Q6 (join no eDoc de 5M de linhas) leva dezenas de segundos
        int maxLinhas = 500)
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
            ConnectionTimeout = 6, // connect curto: falha rápido e deixa o retry/próximo ciclo tentar
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(string sql, CancellationToken cancellationToken = default)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);

        for (var tentativa = 1; ; tentativa++)
        {
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
                if (tentativa < MaxTentativas && EhTransitorio(ex))
                {
                    LimparPool();
                    await Task.Delay(TimeSpan.FromSeconds(IntervaloSegundos), cancellationToken);
                    continue;
                }

                return ResultadoConsulta.ComErro(EhTransitorio(ex)
                    ? "Falha de conexão com a base (rede/VPN instável)."
                    : PrimeiraLinha(ex.Message));
            }
        }
    }

    /// <summary>Mensagem curta (1ª linha, sem stack) para o campo <c>oracle.ultimoErro</c> do snapshot.</summary>
    private static string PrimeiraLinha(string mensagem)
    {
        var quebra = mensagem.IndexOfAny(['\r', '\n']);
        var linha = quebra >= 0 ? mensagem[..quebra] : mensagem;
        return linha.Length > 200 ? linha[..200] : linha;
    }

    private static bool EhTransitorio(OracleException ex) =>
        CodigosTransitorios.Contains(ex.Number)
        || ex.Message.Contains("ORA-50201", StringComparison.Ordinal)
        || ex.Message.Contains("ORA-50000", StringComparison.Ordinal)
        || ex.Message.Contains("ORA-12170", StringComparison.Ordinal);

    /// <summary>Descarta conexões pooled mortas (após o túnel piscar) pra a próxima tentativa abrir uma nova.</summary>
    private void LimparPool()
    {
        try
        {
            using var c = new OracleConnection(_connectionString);
            OracleConnection.ClearPool(c);
        }
        catch { /* best-effort */ }
    }
}
