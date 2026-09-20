using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Core.Integracoes.SisregWeb.Estatisticas;

/// <summary>
/// Quais logins do SISREG entram nas estatísticas de operador. Mora no <c>ParametrosJson</c> da
/// credencial <c>sisreg</c> (mesmo lugar do horário da releitura da fila) — sem tabela nova.
///
/// <para><b>Por que só a lista de habilitados.</b> Decidido em 15/09/2026: "todos os operadores
/// que já apareceram" não precisa ser guardado, sai na hora do export da agenda (coluna
/// <c>operador_autorizador</c>). O que precisa de memória é a escolha de quem entra. Começa
/// <b>vazia</b>: nenhum login entra sem alguém marcar — 35% das autorizações de 2026 são de logins
/// de unidade marcando a própria agenda, e contá-los como regulador distorceria o ranking.</para>
/// </summary>
public static class OperadoresDaEstatistica
{
    public const string Chave = "estatisticaOperadoresHabilitados";

    public static string Normalizar(string? login) => (login ?? string.Empty).Trim().ToUpperInvariant();

    public static IReadOnlySet<string> Ler(JsonObject? json)
    {
        var saida = new HashSet<string>(StringComparer.Ordinal);
        if (json?[Chave] is JsonArray lista)
        {
            foreach (var item in lista)
            {
                if (item is JsonValue v && v.TryGetValue<string>(out var s) && Normalizar(s) is { Length: > 0 } l)
                {
                    saida.Add(l);
                }
            }
        }
        return saida;
    }

    public static Task<IReadOnlySet<string>> ObterAsync(
        IIntegracaoCredencialService credenciais, CancellationToken ct) =>
        ObterAsync(credenciais, SisregWebSessao.Provedor, ct);

    /// <summary>Mesma regra para outro provedor: as estatísticas do SER e do SERNIT guardam os seus
    /// habilitados na credencial <c>ser</c>/<c>sernit</c>, sob a mesma chave.</summary>
    public static async Task<IReadOnlySet<string>> ObterAsync(
        IIntegracaoCredencialService credenciais, string provedor, CancellationToken ct)
    {
        try
        {
            var atual = await credenciais.ObterAsync(provedor, ct);
            return Ler(Parse(atual.ParametrosJson));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem credencial legível, ninguém está habilitado: a tela diz para configurar.
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public static Task<IReadOnlySet<string>> SalvarAsync(
        IIntegracaoCredencialService credenciais, IEnumerable<string>? logins, CancellationToken ct) =>
        SalvarAsync(credenciais, SisregWebSessao.Provedor, logins, ct);

    public static async Task<IReadOnlySet<string>> SalvarAsync(
        IIntegracaoCredencialService credenciais, string provedor, IEnumerable<string>? logins, CancellationToken ct)
    {
        var normalizados = (logins ?? [])
            .Select(Normalizar)
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Merge: usuário, senha e os agendamentos vivem no MESMO ParametrosJson — sobrescrever o JSON
        // inteiro apagaria a credencial de acesso.
        var atual = await credenciais.ObterAsync(provedor, ct);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[Chave] = new JsonArray([.. normalizados.Select(l => (JsonNode?)JsonValue.Create(l))]);

        await credenciais.AtualizarAsync(
            provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            ct);

        return Ler(json);
    }

    private static JsonObject? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            return JsonNode.Parse(texto) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
