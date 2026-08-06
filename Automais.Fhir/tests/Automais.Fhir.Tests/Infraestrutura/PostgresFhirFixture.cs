using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Automais.Fhir.Data;

namespace Automais.Fhir.Tests.Infraestrutura;

/// <summary>
/// Fixture xUnit que sobe um Postgres real com as migrations do hub aplicadas — os testes de
/// upsert dependem do índice único parcial de identifier, que provider InMemory não impõe.
/// Compartilhada por collection.
///
/// <para><b>Sem Docker?</b> Definir <c>FHIR_TESTS_CONNECTION</c> aponta a fixture para um
/// Postgres já existente em vez de subir container. É o que permite rodar esta suíte na máquina
/// de desenvolvimento (que não tem Docker) contra a bancada — um cluster SEPARADO do de produção.
/// A connection string tem de apontar para uma base descartável: a fixture roda
/// <c>MigrateAsync</c> nela e os testes deixam linhas para trás.</para>
/// </summary>
public sealed class PostgresFhirFixture : IAsyncLifetime
{
    private const string VarConexaoExterna = "FHIR_TESTS_CONNECTION";

    private static string? ConexaoExterna =>
        Environment.GetEnvironmentVariable(VarConexaoExterna) is { Length: > 0 } cs ? cs : null;

    private readonly PostgreSqlContainer? _container = ConexaoExterna is not null
        ? null
        : new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("fhirdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public FhirDbContext CriarContexto()
    {
        // Espelha o Program.cs do hub: schema fhir no search path + history table própria.
        var csb = new NpgsqlConnectionStringBuilder(ConexaoExterna ?? _container!.GetConnectionString())
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
        if (_container is not null) await _container.StartAsync();
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresFhirCollection))]
public sealed class PostgresFhirCollection : ICollectionFixture<PostgresFhirFixture>;
