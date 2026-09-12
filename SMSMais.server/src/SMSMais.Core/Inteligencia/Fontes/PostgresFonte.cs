using System.Data;
using Npgsql;
using SMSMais.Core.Inteligencia.Validacao;

namespace SMSMais.Core.Inteligencia.Fontes;

/// <summary>
/// Fonte de dados PostgreSQL — hoje, o <b>próprio banco do SMSMais</b> visto por uma conta de
/// leitura restrita (as fontes "Regulação" e "Atendimento" da Consulta Inteligente). Mesmo
/// contrato da <see cref="SaluxOracleFonte"/>, com três travas independentes:
/// <list type="number">
/// <item>o SQL passa pelo <see cref="SqlReadOnlyGuard"/> (um SELECT/WITH só, sem token de escrita);</item>
/// <item>a sessão nasce <c>default_transaction_read_only=on</c> e a consulta roda dentro de uma
/// transação <c>READ ONLY</c> que é sempre desfeita — escrita falha no banco também;</item>
/// <item>a conta cadastrada na <c>ia_fonte</c> só tem <c>SELECT</c> nas tabelas liberadas para
/// aquela fonte. É ela que decide o que a IA enxerga; as duas primeiras só garantem que nada muda.</item>
/// </list>
/// <c>statement_timeout</c> no servidor e cap de linhas no leitor: é o banco de produção.
/// </summary>
public sealed class PostgresFonte : IFonteDados
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSegundos;
    private readonly int _maxLinhas;

    public PostgresFonte(
        string host,
        int porta,
        string database,
        string usuario,
        string senha,
        int commandTimeoutSegundos = 30,
        int maxLinhas = 1000)
    {
        _commandTimeoutSegundos = commandTimeoutSegundos;
        _maxLinhas = maxLinhas;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = porta,
            Database = database,
            Username = usuario,
            Password = senha,
            // Interativo: conectar falha rápido em vez de pendurar o operador.
            Timeout = 6,
            CommandTimeout = commandTimeoutSegundos,
            // O banco gerenciado exige TLS; um Postgres local sem TLS continua funcionando.
            SslMode = SslMode.Prefer,
            ApplicationName = "smsmais-consulta-inteligente",
            // Pool pequeno: é conta de consulta, não pode disputar conexões com a aplicação.
            MaxPoolSize = 3,
            // O servidor corta a consulta mesmo se o cliente sumir; e a sessão inteira é leitura.
            Options = $"-c default_transaction_read_only=on -c statement_timeout={commandTimeoutSegundos * 1000}",
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task<ResultadoConsulta> ExecutarAsync(
        string sql, CancellationToken cancellationToken = default, int? maxLinhasOverride = null)
    {
        SqlReadOnlyGuard.GarantirLeitura(sql);
        var maxLinhas = maxLinhasOverride ?? _maxLinhas;

        try
        {
            await using var conexao = new NpgsqlConnection(_connectionString);
            await conexao.OpenAsync(cancellationToken);

            // Transação READ ONLY explícita, nunca confirmada: o Dispose desfaz.
            await using var transacao = await conexao.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            await using (var roCmd = new NpgsqlCommand("SET TRANSACTION READ ONLY", conexao, transacao))
            {
                await roCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var cmd = new NpgsqlCommand(sql, conexao, transacao)
            {
                CommandTimeout = _commandTimeoutSegundos,
            };
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var colunas = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                colunas[i] = reader.GetName(i);
            }

            var linhas = new List<IReadOnlyList<object?>>();
            while (linhas.Count < maxLinhas && await reader.ReadAsync(cancellationToken))
            {
                var linha = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    linha[i] = LerValor(reader, i);
                }

                linhas.Add(linha);
            }

            return new ResultadoConsulta(true, colunas, linhas);
        }
        catch (PostgresException ex)
        {
            // Erro do banco (coluna inexistente, permissão negada, timeout): a IA lê e ajusta.
            return ResultadoConsulta.ComErro(ex.SqlState == PostgresErrorCodes.InsufficientPrivilege
                ? $"{ex.MessageText} — esta base só libera algumas tabelas e colunas; consulte o conhecimento da base."
                : ex.MessageText);
        }
        catch (NpgsqlException ex) when (ex is not PostgresException)
        {
            return ResultadoConsulta.ComErro(
                $"Falha de conexão com a base. Tente novamente em instantes. ({ex.Message})");
        }
    }

    public async Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default)
    {
        await using var conexao = new NpgsqlConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand("SELECT 1", conexao) { CommandTimeout = _commandTimeoutSegundos };
        var resultado = await cmd.ExecuteScalarAsync(cancellationToken);
        return resultado is not null;
    }

    /// <summary>
    /// Tipo que o Npgsql não sabe materializar (ex.: <c>vector</c> sem o plugin) não pode derrubar
    /// a consulta inteira: vira texto.
    /// </summary>
    private static object? LerValor(NpgsqlDataReader reader, int i)
    {
        if (reader.IsDBNull(i))
        {
            return null;
        }

        try
        {
            return reader.GetValue(i);
        }
        catch (Exception ex) when (ex is InvalidCastException or NotSupportedException)
        {
            return $"({reader.GetDataTypeName(i)})";
        }
    }
}
