using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace SMSMarica.Data;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultDb";

    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} não configurada.");

        // Garante search_path = smsmarica para que o tipo `vector` (pgvector, instalado no schema
        // smsmarica) resolva em migrations e em runtime. O cluster gerenciado não possui `public`.
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = SmsMaricaDbContext.SchemaPadrao,
        };
        // Teto do pool: o banco gerenciado tem poucas conexões compartilhadas entre apps.
        // Sem teto, o default do Npgsql (100) deixa um app sozinho estourar o limite. Defina
        // ConnectionStrings:DefaultDb com Maximum Pool Size, ou via Db:MaxPoolSize.
        if (int.TryParse(configuration["Db:MaxPoolSize"], out var maxPool) && maxPool > 0)
            csb.MaxPoolSize = maxPool;
        connectionString = csb.ConnectionString;

        void Configurar(DbContextOptionsBuilder options) =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(SmsMaricaDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__migrations", SmsMaricaDbContext.SchemaPadrao);
                npgsql.UseVector(); // pgvector — embeddings do módulo IA
            });

        // DbContext scoped (uso geral) + DbContextFactory (IaService isola um contexto por fonte
        // no processamento paralelo — DbContext não é thread-safe). Opções singleton para coexistirem.
        services.AddDbContext<SmsMaricaDbContext>(Configurar, optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<SmsMaricaDbContext>(Configurar);

        return services;
    }
}
