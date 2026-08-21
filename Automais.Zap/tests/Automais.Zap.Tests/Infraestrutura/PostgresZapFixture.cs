using Automais.Zap.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Automais.Zap.Tests.Infraestrutura;

/// <summary>
/// Postgres real com as migrations do relay aplicadas. Postgres de verdade e não InMemory
/// porque o roteamento depende do índice único de <c>phone_number_id</c> — a garantia de que
/// dois destinos não reivindicam o mesmo número é do banco, e é isso que precisa ser testado.
///
/// <para><b>Sem Docker?</b> <c>ZAP_TESTS_CONNECTION</c> aponta a fixture para um Postgres já
/// existente em vez de subir container. A base tem de ser descartável: a fixture roda
/// <c>MigrateAsync</c> e os testes deixam linhas para trás.</para>
/// </summary>
public sealed class PostgresZapFixture : IAsyncLifetime
{
    private const string VarConexaoExterna = "ZAP_TESTS_CONNECTION";

    private static string? ConexaoExterna =>
        Environment.GetEnvironmentVariable(VarConexaoExterna) is { Length: > 0 } cs ? cs : null;

    private readonly PostgreSqlContainer? _container = ConexaoExterna is not null
        ? null
        : new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("zapdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public ZapDbContext CriarContexto()
    {
        var csb = new NpgsqlConnectionStringBuilder(ConexaoExterna ?? _container!.GetConnectionString())
        {
            SearchPath = ZapDbContext.Schema,
        };
        var options = new DbContextOptionsBuilder<ZapDbContext>()
            .UseNpgsql(csb.ConnectionString,
                npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", ZapDbContext.Schema))
            .Options;
        return new ZapDbContext(options);
    }

    public async Task InitializeAsync()
    {
        if (_container is not null) await _container.StartAsync();
        await using var db = CriarContexto();
        await db.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS {ZapDbContext.Schema}");
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresZapCollection))]
public sealed class PostgresZapCollection : ICollectionFixture<PostgresZapFixture>;
