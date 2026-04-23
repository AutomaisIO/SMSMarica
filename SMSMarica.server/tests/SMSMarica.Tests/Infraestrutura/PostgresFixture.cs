using Microsoft.EntityFrameworkCore;
using SMSMarica.Data;
using Testcontainers.PostgreSql;

namespace SMSMarica.Tests.Infraestrutura;

/// <summary>
/// Fixture xUnit que sobe um container Postgres real para testes de integração.
///
/// Compartilhada por collection — uma instância por execução de teste.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("defaultdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public SmsMaricaDbContext CriarDbContext()
    {
        var options = new DbContextOptionsBuilder<SmsMaricaDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(SmsMaricaDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__migrations", SmsMaricaDbContext.SchemaPadrao);
            })
            .Options;

        return new SmsMaricaDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CriarDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
