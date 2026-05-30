using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Automais.Fhir.Data;

/// <summary>
/// Factory usada apenas em design-time (<c>dotnet ef migrations</c>). Pega a
/// connection string do env var <c>FHIR_DB</c> ou usa um placeholder local —
/// gerar migration não contata o banco.
/// </summary>
public sealed class FhirDbContextFactory : IDesignTimeDbContextFactory<FhirDbContext>
{
    public FhirDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("FHIR_DB")
                   ?? "Host=localhost;Database=defaultdb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<FhirDbContext>()
            .UseNpgsql(conn, npg => npg.MigrationsHistoryTable("__EFMigrationsHistory", FhirDbContext.Schema))
            .Options;

        return new FhirDbContext(options);
    }
}
