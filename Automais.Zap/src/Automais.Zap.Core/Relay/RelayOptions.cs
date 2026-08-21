namespace Automais.Zap.Core.Relay;

public sealed class RelayOptions
{
    public const string Secao = "Relay";

    /// <summary>
    /// Teto por entrega. Curto de propósito: a Meta tem sua própria janela para receber o
    /// 200, e é ela que decide se reentrega. Esperar demais por uma instância travada só
    /// atrasa as outras do mesmo lote.
    /// </summary>
    public int TimeoutSegundos { get; set; } = 10;

    /// <summary>
    /// URL pública deste relay, usada para preencher a Callback URL na tela da Meta.
    /// Não dá para derivar do Request: o painel roda num host e o webhook em outro.
    /// </summary>
    public string? UrlWebhookPublica { get; set; }

    /// <summary>Dias de retenção do <c>entrega_log</c>. É trilha operacional, não histórico.</summary>
    public int RetencaoLogDias { get; set; } = 30;
}
