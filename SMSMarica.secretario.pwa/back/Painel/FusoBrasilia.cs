namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Regra única de fuso do repo: timestamps do snapshot são <see cref="DateTimeOffset"/>
/// no fuso America/Sao_Paulo (Brasília, fixo −03:00 desde o fim do horário de verão).
/// Fallback para o id Windows quando o ICU não resolve o id IANA.
/// </summary>
public static class FusoBrasilia
{
    private static readonly TimeZoneInfo Fuso = Resolver();

    private static TimeZoneInfo Resolver()
    {
        // Fallback defensivo: host sem tzdata/libicu (container mínimo, invariant
        // globalization) não pode derrubar o serviço no startup. Se nem o id IANA nem o
        // Windows resolverem, degrada para offset fixo −03:00 — Brasília não tem horário
        // de verão desde 2019, então o comportamento é idêntico na prática.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (Exception)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (Exception)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "America/Sao_Paulo-fixo", TimeSpan.FromHours(-3),
                    "Brasília (offset fixo -03:00)", "Brasília (offset fixo -03:00)");
            }
        }
    }

    /// <summary>Agora em Brasília (offset embutido).</summary>
    public static DateTimeOffset Agora() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);
}
