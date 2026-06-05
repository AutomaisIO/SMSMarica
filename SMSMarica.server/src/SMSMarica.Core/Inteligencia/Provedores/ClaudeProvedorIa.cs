using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;

namespace SMSMarica.Core.Inteligencia.Provedores;

/// <summary>
/// Provedor de IA falando direto com a Anthropic Messages API (HttpClient cru, sem SDK).
/// Usa saída estruturada (<c>output_config.format = json_schema</c>) para devolver
/// SQL + plano de visualização, e prompt caching (<c>cache_control: ephemeral</c>) no bloco
/// de instruções fixas do <c>system</c>. Token e modelo vêm da configuração global cifrada.
/// </summary>
public sealed class ClaudeProvedorIa(
    HttpClient http,
    SmsMaricaDbContext db,
    IProtetorSegredos protetor) : IProvedorIa
{
    public const string HttpClientName = "IaProvedor";

    private const string EndpointMensagens = "v1/messages";
    private const string VersaoApi = "2023-06-01";
    private const string ModeloPadrao = "claude-opus-4-8";
    private const int MaxTokens = 4096;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<GeracaoConsultaResultado> GerarConsultaAsync(
        GeracaoConsultaContexto contexto, CancellationToken cancellationToken = default)
    {
        var (token, modelo) = await ObterCredenciaisAsync(cancellationToken);

        var sistema = MontarSystemPrompt(contexto.Dialeto);
        var pergunta = MontarTurnoUsuario(contexto);

        var body = new
        {
            model = modelo,
            max_tokens = MaxTokens,
            system = new object[]
            {
                new
                {
                    type = "text",
                    text = sistema,
                    cache_control = new { type = "ephemeral" },
                },
            },
            output_config = new
            {
                format = new
                {
                    type = "json_schema",
                    schema = SchemaSaida(),
                },
            },
            messages = new object[]
            {
                new { role = "user", content = pergunta },
            },
        };

        using var doc = await EnviarAsync(token, body, cancellationToken);
        var root = doc.RootElement;

        var (tokensEntrada, tokensSaida) = LerUsage(root);
        var textoJson = ExtrairTexto(root);

        SaidaEstruturada saida;
        try
        {
            saida = JsonSerializer.Deserialize<SaidaEstruturada>(textoJson, JsonOpts)
                ?? throw new ConflitoException("ia.provedor", "Saída estruturada vazia.");
        }
        catch (JsonException ex)
        {
            throw new ConflitoException("ia.provedor",
                $"Não foi possível interpretar a saída estruturada da IA: {ex.Message}");
        }

        return new GeracaoConsultaResultado(
            Sql: saida.Sql ?? string.Empty,
            Visualizacao: saida.Visualizacao ?? "tabela",
            Titulo: saida.Titulo ?? string.Empty,
            Resumo: saida.Resumo,
            EixoX: saida.EixoX,
            EixoY: saida.EixoY,
            InstrucaoAprendizado: saida.InstrucaoAprendizado,
            TokensEntrada: tokensEntrada,
            TokensSaida: tokensSaida);
    }

    public async Task<string> ResumirResultadoAsync(
        string pergunta, IReadOnlyList<string> colunas,
        IReadOnlyList<IReadOnlyList<object?>> linhas, CancellationToken cancellationToken = default)
    {
        var (token, modelo) = await ObterCredenciaisAsync(cancellationToken);

        var tabela = SerializarTabela(colunas, linhas);
        var prompt =
            $"Pergunta do usuário: {pergunta}\n\n" +
            $"Resultado da consulta (colunas + até algumas linhas):\n{tabela}\n\n" +
            "Escreva um resumo curto e claro em português (pt-BR), em linguagem acessível a " +
            "um usuário leigo, explicando o que os dados respondem à pergunta. Não invente " +
            "números que não estejam no resultado. Responda apenas com o resumo, sem preâmbulo.";

        var body = new
        {
            model = modelo,
            max_tokens = MaxTokens,
            messages = new object[]
            {
                new { role = "user", content = prompt },
            },
        };

        using var doc = await EnviarAsync(token, body, cancellationToken);
        return ExtrairTexto(doc.RootElement).Trim();
    }

    // ---- HTTP ----

    private async Task<JsonDocument> EnviarAsync(
        string token, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);

        using var req = new HttpRequestMessage(HttpMethod.Post, EndpointMensagens)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-api-key", token);
        req.Headers.Add("anthropic-version", VersaoApi);

        using var resp = await http.SendAsync(req, cancellationToken);
        var corpo = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            throw new ConflitoException(
                "ia.provedor",
                $"Provedor de IA retornou {(int)resp.StatusCode}: {corpo}");
        }

        return JsonDocument.Parse(corpo);
    }

    private async Task<(string Token, string Modelo)> ObterCredenciaisAsync(CancellationToken cancellationToken)
    {
        var config = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new ValidacaoException("ia.config", "Configuração do módulo IA não definida.");

        if (string.IsNullOrWhiteSpace(config.TokenCifrado))
        {
            throw new ValidacaoException("ia.config.token", "Token do provedor de IA não configurado.");
        }

        var token = protetor.Revelar(config.TokenCifrado);
        var modelo = string.IsNullOrWhiteSpace(config.Modelo) ? ModeloPadrao : config.Modelo;
        return (token, modelo);
    }

    // ---- Parsing ----

    private static (int Entrada, int Saida) LerUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
        {
            return (0, 0);
        }

        var entrada = usage.TryGetProperty("input_tokens", out var i) && i.TryGetInt32(out var iv) ? iv : 0;
        var saida = usage.TryGetProperty("output_tokens", out var o) && o.TryGetInt32(out var ov) ? ov : 0;
        return (entrada, saida);
    }

    private static string ExtrairTexto(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new ConflitoException("ia.provedor", "Resposta da IA sem bloco de conteúdo.");
        }

        var sb = new StringBuilder();
        foreach (var bloco in content.EnumerateArray())
        {
            if (bloco.TryGetProperty("type", out var t) && t.GetString() == "text"
                && bloco.TryGetProperty("text", out var texto))
            {
                sb.Append(texto.GetString());
            }
        }

        var resultado = sb.ToString();
        if (string.IsNullOrWhiteSpace(resultado))
        {
            throw new ConflitoException("ia.provedor", "Resposta da IA sem texto.");
        }

        return resultado;
    }

    // ---- Prompt ----

    private static string MontarSystemPrompt(string dialeto) =>
        "Você é um gerador de consultas SQL para um hospital. A partir de uma pergunta em " +
        "linguagem natural, você gera EXATAMENTE UM comando SELECT read-only (pode usar WITH/CTE), " +
        $"no dialeto {dialeto}. Regras invioláveis:\n" +
        "- NUNCA gere INSERT, UPDATE, DELETE, MERGE, DROP, ALTER, CREATE, GRANT, REVOKE, TRUNCATE, " +
        "CALL, EXECUTE, BEGIN, DECLARE ou múltiplos statements separados por ';'.\n" +
        "- Use apenas o conhecimento recuperado (tabelas/colunas) e os aprendizados informados.\n" +
        "- Limite resultados a um volume razoável quando fizer sentido.\n" +
        "- Escolha 'visualizacao' entre: numero, lista, tabela, grafico_pizza, grafico_barra, grafico_linha. " +
        "Use 'numero' para um único valor agregado; 'tabela' para múltiplas colunas; gráficos quando houver " +
        "uma dimensão (eixoX) e uma medida (eixoY).\n" +
        "- 'titulo' é um título curto para a resposta; 'resumo' é opcional.\n" +
        "- Quando estiver corrigindo um SQL que falhou, preencha 'instrucaoAprendizado' com uma instrução " +
        "curta e reaproveitável (ex.: nome correto de tabela/coluna) que evite o erro no futuro.";

    private static string MontarTurnoUsuario(GeracaoConsultaContexto contexto)
    {
        var sb = new StringBuilder();
        sb.Append("Pergunta: ").AppendLine(contexto.Pergunta);

        if (!string.IsNullOrWhiteSpace(contexto.ConhecimentoRecuperado))
        {
            sb.AppendLine().AppendLine("# Conhecimento recuperado").AppendLine(contexto.ConhecimentoRecuperado);
        }

        if (!string.IsNullOrWhiteSpace(contexto.AprendizadosAtivos))
        {
            sb.AppendLine().AppendLine("# Aprendizados ativos").AppendLine(contexto.AprendizadosAtivos);
        }

        if (!string.IsNullOrWhiteSpace(contexto.SqlAnterior) || !string.IsNullOrWhiteSpace(contexto.ErroAnterior))
        {
            sb.AppendLine().AppendLine("# Correção necessária");
            if (!string.IsNullOrWhiteSpace(contexto.SqlAnterior))
            {
                sb.AppendLine("SQL anterior (falhou):").AppendLine(contexto.SqlAnterior);
            }

            if (!string.IsNullOrWhiteSpace(contexto.ErroAnterior))
            {
                sb.AppendLine("Erro retornado pelo banco:").AppendLine(contexto.ErroAnterior);
            }

            sb.AppendLine("Gere um novo SELECT corrigido e preencha 'instrucaoAprendizado'.");
        }

        return sb.ToString();
    }

    private static object SchemaSaida() => new
    {
        type = "object",
        properties = new
        {
            sql = new { type = "string" },
            visualizacao = new
            {
                type = "string",
                @enum = new[] { "numero", "lista", "tabela", "grafico_pizza", "grafico_barra", "grafico_linha" },
            },
            titulo = new { type = "string" },
            resumo = new { type = new[] { "string", "null" } },
            eixoX = new { type = new[] { "string", "null" } },
            eixoY = new { type = new[] { "string", "null" } },
            instrucaoAprendizado = new { type = new[] { "string", "null" } },
        },
        required = new[] { "sql", "visualizacao", "titulo", "resumo", "eixoX", "eixoY", "instrucaoAprendizado" },
        additionalProperties = false,
    };

    private static string SerializarTabela(
        IReadOnlyList<string> colunas, IReadOnlyList<IReadOnlyList<object?>> linhas)
    {
        const int maxLinhas = 50;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", colunas));

        var contagem = Math.Min(linhas.Count, maxLinhas);
        for (var i = 0; i < contagem; i++)
        {
            sb.AppendLine(string.Join(" | ", linhas[i].Select(v => v?.ToString() ?? string.Empty)));
        }

        if (linhas.Count > maxLinhas)
        {
            sb.AppendLine($"... (+{linhas.Count - maxLinhas} linhas)");
        }

        return sb.ToString();
    }

    private sealed record SaidaEstruturada(
        [property: JsonPropertyName("sql")] string? Sql,
        [property: JsonPropertyName("visualizacao")] string? Visualizacao,
        [property: JsonPropertyName("titulo")] string? Titulo,
        [property: JsonPropertyName("resumo")] string? Resumo,
        [property: JsonPropertyName("eixoX")] string? EixoX,
        [property: JsonPropertyName("eixoY")] string? EixoY,
        [property: JsonPropertyName("instrucaoAprendizado")] string? InstrucaoAprendizado);
}
