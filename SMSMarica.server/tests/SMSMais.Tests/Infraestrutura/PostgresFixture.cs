using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using Testcontainers.PostgreSql;

namespace SMSMais.Tests.Infraestrutura;

/// <summary>
/// Fixture xUnit que sobe um Postgres real para testes de integração.
///
/// Compartilhada por collection — uma instância por execução de teste.
///
/// <para><b>Sem Docker?</b> Definir <c>SMSMARICA_TESTS_CONNECTION</c> aponta a fixture para um
/// Postgres já existente em vez de subir container. É o que permite rodar esta suíte na máquina
/// de desenvolvimento (que não tem Docker) contra a bancada — um cluster SEPARADO do de produção.
/// A connection string tem de apontar para uma base descartável: a fixture roda
/// <c>MigrateAsync</c> nela e os testes deixam linhas para trás.</para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string VarConexaoExterna = "SMSMARICA_TESTS_CONNECTION";

    private static string? ConexaoExterna =>
        Environment.GetEnvironmentVariable(VarConexaoExterna) is { Length: > 0 } cs ? cs : null;

    private readonly PostgreSqlContainer? _container = ConexaoExterna is not null
        ? null
        : new PostgreSqlBuilder()
            // pgvector, não o postgres puro: a migration AddInteligenciaIa cria a extensão
            // `vector` (IaAprendizado.Embedding é vector(1024)) e a imagem oficial do Postgres
            // não a traz — `extension "vector" is not available` derruba a fixture antes do
            // primeiro teste. Só aparece com Docker; quem roda pela bancada
            // (SMSMARICA_TESTS_CONNECTION) tem a extensão no cluster e não vê o problema.
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("defaultdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public string ConnectionString => ConexaoExterna ?? _container!.GetConnectionString();

    public SmsMaisDbContext CriarDbContext()
    {
        var options = new DbContextOptionsBuilder<SmsMaisDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(SmsMaisDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__migrations", SmsMaisDbContext.SchemaPadrao);
                // Espelha o DependencyInjection da Data: sem isto o modelo nem valida
                // (IaAprendizado.Embedding é vector(1024)) e a fixture morre antes do 1º teste.
                npgsql.UseVector();
            })
            .Options;

        return new SmsMaisDbContext(options);
    }

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            await CriarExtensaoVetorAsync();
        }

        await using var db = CriarDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Cria a extensão <c>vector</c> no <c>public</c> ANTES das migrations.
    ///
    /// <para>A migration <c>AddInteligenciaIa</c> declara a extensão no schema <c>smsmarica</c>
    /// (<c>Npgsql:PostgresExtension:smsmarica.vector</c>), mas as colunas são <c>vector(1024)</c>
    /// <b>sem qualificar</b> — e o <c>search_path</c> da conexão não inclui <c>smsmarica</c>. Em
    /// produção isso passa porque lá a extensão está no <c>public</c> e o tipo resolve; num
    /// container limpo não está em lugar nenhum e o <c>CREATE TABLE</c> morre em
    /// <c>type "vector" does not exist</c>. Aqui a bancada de container passa a espelhar
    /// produção.</para>
    ///
    /// <para>Quem roda por <c>SMSMARICA_TESTS_CONNECTION</c> não passa por aqui: naquele cluster a
    /// extensão já existe, e um banco emprestado não é lugar de criar extensão por conta própria.</para>
    /// </summary>
    private async Task CriarExtensaoVetorAsync()
    {
        await using var conexao = new Npgsql.NpgsqlConnection(ConnectionString);
        await conexao.OpenAsync();
        await using var comando = new Npgsql.NpgsqlCommand(
            "CREATE EXTENSION IF NOT EXISTS vector;", conexao);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
