using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;
using Testcontainers.PostgreSql;

namespace SMSMarica.Tests.Infraestrutura;

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
            .WithImage("postgres:16-alpine")
            .WithDatabase("defaultdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public string ConnectionString => ConexaoExterna ?? _container!.GetConnectionString();

    public SmsMaricaDbContext CriarDbContext()
    {
        var options = new DbContextOptionsBuilder<SmsMaricaDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(SmsMaricaDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__migrations", SmsMaricaDbContext.SchemaPadrao);
                // Espelha o DependencyInjection da Data: sem isto o modelo nem valida
                // (IaAprendizado.Embedding é vector(1024)) e a fixture morre antes do 1º teste.
                npgsql.UseVector();
            })
            .Options;

        return new SmsMaricaDbContext(options);
    }

    public async Task InitializeAsync()
    {
        if (_container is not null) await _container.StartAsync();
        await using var db = CriarDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
