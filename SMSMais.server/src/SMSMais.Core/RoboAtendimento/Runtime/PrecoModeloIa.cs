namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Preço por milhão de tokens, para estimar o custo de cada turno do robô. A Messages API devolve
/// <c>usage</c> (tokens) mas <b>não</b> o custo — diferente do SDK da assinatura, que entregava
/// <c>total_cost_usd</c> pronto. Sem esta tabela o relatório de consumo por assunto perderia a
/// coluna de custo.
///
/// É uma ESTIMATIVA de tabela pública, não a fatura: leituras de cache custam menos que entrada
/// nova e aqui são cobradas pelo preço cheio de entrada, então o número tende a ser conservador
/// (para cima). Serve para comparar assuntos e vigiar ordem de grandeza — a fatura da Anthropic
/// continua sendo a fonte da verdade.
/// </summary>
public static class PrecoModeloIa
{
    /// <summary>(entrada, saída) em USD por 1 milhão de tokens. Prefixo do modelo → preço.</summary>
    private static readonly (string Prefixo, decimal Entrada, decimal Saida)[] Tabela =
    [
        ("claude-haiku-4-5", 1.00m, 5.00m),
        ("claude-sonnet-4-5", 3.00m, 15.00m),
        ("claude-sonnet-5", 3.00m, 15.00m),
        ("claude-opus-4", 15.00m, 75.00m),
        ("claude-opus-5", 15.00m, 75.00m),
        ("claude-haiku", 1.00m, 5.00m),
        ("claude-sonnet", 3.00m, 15.00m),
        ("claude-opus", 15.00m, 75.00m),
    ];

    /// <summary>Custo estimado do turno. <c>null</c> quando o modelo não está na tabela — melhor
    /// não ter número do que ter um número inventado.</summary>
    public static decimal? Calcular(string? modelo, long tokensEntrada, long tokensSaida)
    {
        if (string.IsNullOrWhiteSpace(modelo)) return null;
        var m = modelo.Trim();

        foreach (var (prefixo, entrada, saida) in Tabela)
        {
            if (!m.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) continue;
            var custo = (tokensEntrada * entrada + tokensSaida * saida) / 1_000_000m;
            return decimal.Round(custo, 6, MidpointRounding.AwayFromZero);
        }
        return null;
    }
}
