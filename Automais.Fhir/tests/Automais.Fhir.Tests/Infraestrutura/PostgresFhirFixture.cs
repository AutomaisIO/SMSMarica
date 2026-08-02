using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Automais.Fhir.Data;

namespace Automais.Fhir.Tests.Infraestrutura;

/// <summary>
/// Fixture xUnit que sobe um Postgres real (Testcontainers) com as migrations do hub
/// aplicadas — os testes de upsert dependem do índice único parcial de identifier,
/// que provider InMemory não impõe. Compartilhada por collection.
/// </summary>
public sealed class PostgresFhirFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("fhirdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public FhirDbContext CriarContexto()
    {
        // Espelha o Program.cs do hub: schema fhir no search path + history table própria.
        var csb = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            SearchPath = "fhir, public",
        };
        var options = new DbContextOptionsBuilder<FhirDbContext>()
            .UseNpgsql(csb.ConnectionString,
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", FhirDbContext.Schema))
            .Options;
        return new FhirDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(nameof(PostgresFhirCollection))]
public sealed class PostgresFhirCollection : ICollectionFixture<PostgresFhirFixture>;
