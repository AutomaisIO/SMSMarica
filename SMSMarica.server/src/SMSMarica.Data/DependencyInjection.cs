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
        // Teto do pool: o banco gerenciado tem POUCAS conexões (max_connections=25 no DO),
        // compartilhadas entre apps + processos internos do cluster. Sem teto, o default do
        // Npgsql (100) deixa um app sozinho estourar o limite → "remaining connection slots
        // are reserved..." → 500 intermitente. Precedência: Db:MaxPoolSize (config) >
        // "Maximum Pool Size" embutido na connection string > default seguro abaixo.
        const int tetoPadraoPool = 10;
        if (int.TryParse(configuration["Db:MaxPoolSize"], out var maxPool) && maxPool > 0)
            csb.MaxPoolSize = maxPool;
        else if (connectionString.IndexOf("Pool Size", StringComparison.OrdinalIgnoreCase) < 0)
            csb.MaxPoolSize = tetoPadraoPool;

        // Devolve conexões ociosas ao cluster mais rápido após picos (default do Npgsql é 300s).
        if (connectionString.IndexOf("Connection Idle Lifetime", StringComparison.OrdinalIgnoreCase) < 0)
            csb.ConnectionIdleLifetime = 60;

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
