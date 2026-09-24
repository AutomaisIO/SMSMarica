using System.Net;

namespace SMSMais.Core.Alertas;

/// <summary>
/// Reconhece quando a Anthropic recusou por problema da CONTA (crédito, chave, permissão) — o
/// tipo de falha que não passa sozinho e para todos os usos de IA de uma vez. Foi assim que o
/// robô ficou mudo sem ninguém saber: o crédito acabou e cada tarefa só virou um Warning no log.
/// </summary>
public static class FalhaContaIa
{
    /// <returns>Descrição para o operador, ou null se não é problema de conta.</returns>
    public static string? Classificar(HttpStatusCode status, string? corpo)
    {
        var c = corpo ?? string.Empty;
        if (c.Contains("credit balance", StringComparison.OrdinalIgnoreCase)
            || c.Contains("billing", StringComparison.OrdinalIgnoreCase))
            return "O crédito da conta Anthropic acabou. Recarregue em console.anthropic.com → Billing.";
        // 22/09/2026: "You have reached your specified API usage limits. You will regain access on
        // 2026-10-01" — o teto de gasto mensal da conta. Não era reconhecido, e o robô ficou três
        // dias mudo só com o aviso genérico de tarefa falhada.
        if (EhLimiteDeUso(c))
            return "A conta Anthropic atingiu o limite de gasto configurado. Aumente em "
                + "console.anthropic.com → Settings → Limits (ou espere a data que a mensagem indica).";
        if (status == HttpStatusCode.Unauthorized || c.Contains("authentication_error", StringComparison.Ordinal))
            return "A chave da Anthropic foi recusada (inválida ou revogada). Troque em Configuração da IA.";
        if (status == HttpStatusCode.Forbidden || c.Contains("permission_error", StringComparison.Ordinal))
            return "A chave da Anthropic não tem permissão para esta chamada.";
        return null;
    }

    /// <summary>Mesma checagem sobre a mensagem de uma exceção já montada ("Anthropic retornou 400: …").</summary>
    public static bool EhFalhaDeConta(string? mensagem) =>
        mensagem is not null
        && (mensagem.Contains("credit balance", StringComparison.OrdinalIgnoreCase)
            || EhLimiteDeUso(mensagem)
            || mensagem.Contains("authentication_error", StringComparison.Ordinal)
            || mensagem.Contains("permission_error", StringComparison.Ordinal));

    private static bool EhLimiteDeUso(string texto) =>
        texto.Contains("usage limit", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Pendurado nos HttpClients da Anthropic: toda resposta recusada por problema de conta vira
/// aviso <see cref="AlertaCatalogo.IaConta"/>, venha do robô, do treinamento ou do distribuidor.
/// Um ponto só, em vez de lembrar de avisar em cada chamador.
/// </summary>
public sealed class FalhaContaIaHandler(IAlertaPlataforma alerta) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = await base.SendAsync(request, cancellationToken);
        if (resp.IsSuccessStatusCode || (int)resp.StatusCode >= 500 || resp.StatusCode == HttpStatusCode.TooManyRequests)
            return resp;

        // Bufferiza para ler aqui sem roubar o corpo de quem chamou.
        await resp.Content.LoadIntoBufferAsync(cancellationToken);
        var corpo = await resp.Content.ReadAsStringAsync(cancellationToken);
        var descricao = FalhaContaIa.Classificar(resp.StatusCode, corpo);
        if (descricao is not null)
        {
            alerta.Reportar(new EventoAlerta(
                AlertaCatalogo.IaConta,
                "IA parada: a Anthropic recusou a chamada",
                $"{descricao}\n\nEnquanto isso o robô de atendimento não responde ninguém.\n\n"
                + $"Resposta ({(int)resp.StatusCode}): {(corpo.Length <= 300 ? corpo : corpo[..300])}"));
        }
        return resp;
    }
}
