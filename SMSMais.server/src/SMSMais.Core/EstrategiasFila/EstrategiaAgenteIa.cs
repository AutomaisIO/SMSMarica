using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.EstrategiasFila.Dtos;
using SMSMais.Core.Inteligencia.Provedores;
using SMSMais.Data;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>O que o agente devolveu — ou por que não devolveu.</summary>
public sealed record ResultadoAgenteEstrategia(
    ParametrosEstrategia? ParametrosFinais,
    ProjecaoDto? Projecao,
    PropostaAgenteDto? Proposta,
    string Modelo,
    long TokensEntrada,
    long TokensSaida,
    int DuracaoMs,
    string? Falha);

public interface IEstrategiaAgenteIa
{
    Task<ResultadoAgenteEstrategia> PlanejarAsync(
        CenarioFilaDto cenario, ParametrosEstrategia parametros, CancellationToken ct = default);
}

/// <summary>
/// O agente de estratégias: loop de tool-use no .NET sobre a Messages API (ADR-0050/0058).
///
/// <para><b>Duas ferramentas, uma delas terminal.</b> <c>simular</c> roda o
/// <see cref="SimuladorFila"/> com os números que o modelo mandou (respeitando as travas) e devolve
/// os marcos; <c>propor_estrategia</c> encerra. Prosa sem a terminal é descartada — a resposta é
/// estrutura, não instrução. Trava violada volta como erro de ferramenta; o modelo corrige ou a
/// rodada termina em <see cref="ResultadoAgenteEstrategia.Falha"/>, nunca em 500.</para>
///
/// <para><b>Modelo</b>: o da Configuração da IA (troca pela tela, sem deploy). <c>effort</c> só
/// vai para modelos que o aceitam.</para>
/// </summary>
public sealed class EstrategiaAgenteIa(
    ClienteMessagesApi cliente,
    SmsMaisDbContext db,
    ILogger<EstrategiaAgenteIa> logger) : IEstrategiaAgenteIa
{
    private const int MaxIteracoes = 10;
    private const int MaxTokens = 6000;
    private const string ModeloPadrao = "claude-opus-4-8";
    private const string FerramentaSimular = "simular";
    private const string FerramentaPropor = "propor_estrategia";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoAgenteEstrategia> PlanejarAsync(
        CenarioFilaDto cenario, ParametrosEstrategia parametros, CancellationToken ct = default)
    {
        var relogio = Stopwatch.StartNew();
        var modelo = await ModeloAsync(ct);
        var sistema = EstrategiaPrompt.Montar(cenario, parametros);
        var mensagens = new List<object>
        {
            new
            {
                role = "user",
                content = "Monte a estratégia para este procedimento. Teste com `simular` antes de propor e encerre com `propor_estrategia`.",
            },
        };

        long entrada = 0, saida = 0;
        var simulacoes = 0;
        string? falha = null;
        ParametrosEstrategia? finais = null;
        ProjecaoDto? projecao = null;
        PropostaAgenteDto? proposta = null;

        try
        {
            for (var i = 0; i < MaxIteracoes && proposta is null; i++)
            {
                var turno = await cliente.ChamarAsync(
                    modelo, sistema, mensagens, Ferramentas, Esforco(modelo), MaxTokens, ct);
                entrada += turno.TokensEntrada;
                saida += turno.TokensSaida;

                if (turno.Chamadas.Count == 0)
                {
                    // Prosa sem ferramenta: pede a terminal uma vez; se insistir, encerra.
                    if (i >= MaxIteracoes - 1) break;
                    mensagens.Add(new { role = "assistant", content = turno.Conteudo });
                    mensagens.Add(new
                    {
                        role = "user",
                        content = "Sua resposta só vale pela ferramenta `propor_estrategia`. Chame-a agora com os parâmetros finais.",
                    });
                    continue;
                }

                var resultados = new List<object>(turno.Chamadas.Count);
                foreach (var c in turno.Chamadas)
                {
                    if (c.Nome == FerramentaSimular)
                    {
                        simulacoes++;
                        var (p, violacoes) = ParametrosEstrategiaAplicador.Aplicar(parametros, c.Argumentos);
                        if (violacoes.Count > 0)
                        {
                            resultados.Add(ClienteMessagesApi.ResultadoFerramenta(
                                c.Id, "Parâmetros rejeitados:\n- " + string.Join("\n- ", violacoes), erro: true));
                            continue;
                        }

                        var proj = SimuladorFila.Projetar(p, cenario.Fila.Total);
                        resultados.Add(ClienteMessagesApi.ResultadoFerramenta(c.Id, ResumoDaProjecao(p, proj)));
                        continue;
                    }

                    if (c.Nome == FerramentaPropor)
                    {
                        var args = c.Argumentos;
                        var parametrosProposta = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("parametros", out var pa)
                            ? pa : default;
                        var (p, violacoes) = ParametrosEstrategiaAplicador.Aplicar(parametros, parametrosProposta);
                        if (violacoes.Count > 0)
                        {
                            resultados.Add(ClienteMessagesApi.ResultadoFerramenta(
                                c.Id, "Proposta rejeitada — corrija e chame de novo:\n- " + string.Join("\n- ", violacoes), erro: true));
                            continue;
                        }

                        var lida = LerProposta(args, simulacoes);
                        if (lida is null)
                        {
                            resultados.Add(ClienteMessagesApi.ResultadoFerramenta(
                                c.Id, "Proposta incompleta: informe `resumo` e ao menos uma ação em `acoes`.", erro: true));
                            continue;
                        }

                        finais = p;
                        projecao = SimuladorFila.Projetar(p, cenario.Fila.Total);
                        proposta = lida;
                        resultados.Add(ClienteMessagesApi.ResultadoFerramenta(c.Id, "Registrado."));
                        continue;
                    }

                    resultados.Add(ClienteMessagesApi.ResultadoFerramenta(c.Id, $"Ferramenta desconhecida: {c.Nome}", erro: true));
                }

                if (proposta is not null) break;

                mensagens.Add(new { role = "assistant", content = turno.Conteudo });
                mensagens.Add(new { role = "user", content = resultados });
            }

            if (proposta is null)
                falha = "O agente encerrou sem propor uma estratégia válida (sem chamar `propor_estrategia` ou violando travas até o limite).";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agente de estratégias de fila falhou ({Modelo}).", modelo);
            falha = "Falha ao consultar o modelo: " + ex.Message;
        }

        return new ResultadoAgenteEstrategia(
            finais, projecao, proposta, modelo, entrada, saida, (int)relogio.ElapsedMilliseconds, falha);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<string> ModeloAsync(CancellationToken ct)
    {
        var cfg = await db.IaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(cfg?.Modelo) ? ModeloPadrao : cfg.Modelo.Trim();
    }

    /// <summary><c>effort</c> existe de Opus 4.5 em diante; Haiku 4.5 e Sonnet 4.5 rejeitam.</summary>
    internal static string? Esforco(string modelo)
    {
        var m = modelo.ToLowerInvariant();
        if (m.StartsWith("claude-haiku") || m.StartsWith("claude-sonnet-4-5") || m.StartsWith("claude-3")) return null;
        return "high";
    }

    private static string ResumoDaProjecao(ParametrosEstrategia p, ProjecaoDto proj)
    {
        var marcos = new
        {
            capacidadeSemanal = proj.CapacidadeSemanal,
            vagasSemanais = proj.VagasSemanais,
            entradaSemanal = proj.EntradaSemanal,
            zera = proj.Zera,
            semanaZera = proj.SemanaZera,
            filaFinal = proj.FilaFinal,
            crescimentoSemanal = proj.CrescimentoSemanal,
            capacidadeEquilibrio = proj.CapacidadeEquilibrio,
            capacidadeParaZerarNoPrazo = proj.CapacidadeParaZerarNoPrazo,
            picoFila = proj.PicoFila,
            atendidosAteZerar = proj.AtendidosAteZerar,
            parametrosUsados = new
            {
                unidades = p.Unidades.Valor,
                profissionais = p.Profissionais.Valor,
                turnosPorProfissionalSemana = p.TurnosPorProfissionalSemana.Valor,
                atendimentosPorTurno = p.AtendimentosPorTurno.Valor,
                turnosSemanais = p.TurnosSemanais(),
                aproveitamento = p.Aproveitamento.Valor,
                entradaSemanal = p.EntradaSemanal.Valor,
                mutiroes = p.Mutiroes,
            },
            // Amostra da série para o modelo enxergar a forma da curva sem 104 pontos.
            fila = proj.Serie.Where(s => s.Semana % 4 == 0 || s.Semana == proj.SemanaZera).Select(s => new { s.Semana, s.Fila }),
        };
        return JsonSerializer.Serialize(marcos, Json);
    }

    private static PropostaAgenteDto? LerProposta(JsonElement args, int simulacoes)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        var resumo = Str(args, "resumo");
        if (string.IsNullOrWhiteSpace(resumo)) return null;

        var acoes = new List<AcaoPropostaDto>();
        if (args.TryGetProperty("acoes", out var a) && a.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in a.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var descricao = Str(item, "descricao");
                if (string.IsNullOrWhiteSpace(descricao)) continue;
                double? impacto = item.TryGetProperty("impactoVagasSemana", out var iv) && iv.ValueKind == JsonValueKind.Number
                    ? iv.GetDouble() : null;
                acoes.Add(new AcaoPropostaDto(Str(item, "tipo") ?? "outro", descricao.Trim(), Str(item, "unidade"), impacto));
            }
        }
        if (acoes.Count == 0) return null;

        var riscos = new List<string>();
        if (args.TryGetProperty("riscos", out var r) && r.ValueKind == JsonValueKind.Array)
            riscos.AddRange(r.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!.Trim()).Where(x => x.Length > 0));

        var confianca = args.TryGetProperty("confianca", out var cf) && cf.ValueKind == JsonValueKind.Number
            ? Math.Clamp(cf.GetDouble(), 0, 1) : 0.5;

        return new PropostaAgenteDto(resumo.Trim(), acoes, riscos, Math.Round(confianca, 2), simulacoes);
    }

    private static string? Str(JsonElement o, string campo) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    // ------------------------------------------------------------------ ferramentas

    private static readonly object SchemaParametros = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            unidades = new { type = "number", description = "Nº de unidades executantes (só restrição/rótulo; não entra na capacidade)." },
            profissionais = new { type = "number", description = "Nº de profissionais atendendo o procedimento." },
            turnosPorProfissionalSemana = new { type = "number", description = "Média de turnos (dias com atendimento do procedimento) por semana de cada profissional. Ex.: 1,5 = um médico atende 1 ou 2 dias por semana." },
            atendimentosPorTurno = new { type = "number", description = "Vagas de regulação por turno (por profissional por dia)." },
            aproveitamento = new { type = "number", description = "Fração das vagas que viram atendimento (0–1)." },
            entradaSemanal = new { type = "number", description = "Pessoas novas por semana." },
            mutiroes = new
            {
                type = "array",
                description = "Vagas extras em semanas específicas (semana 1 = próxima semana).",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[] { "semana", "vagas" },
                    properties = new
                    {
                        semana = new { type = "integer" },
                        vagas = new { type = "integer" },
                        descricao = new { type = "string" },
                    },
                },
            },
        },
    };

    private static readonly FerramentaModelo[] Ferramentas =
    [
        new(FerramentaSimular,
            "Roda a projeção determinística da fila com os parâmetros informados (os omitidos mantêm o valor atual). " +
            "Devolve capacidade/semana, se zera e em que semana, equilíbrio e a forma da curva. Chame quantas vezes precisar.",
            SchemaParametros),
        new(FerramentaPropor,
            "TERMINAL. Entrega a estratégia final: parâmetros escolhidos, resumo para o gestor, ações concretas e riscos. " +
            "Chame uma única vez, no fim.",
            new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "parametros", "resumo", "acoes", "riscos", "confianca" },
                properties = new
                {
                    parametros = SchemaParametros,
                    resumo = new { type = "string", description = "3 a 6 frases, português claro, para um gestor." },
                    acoes = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[] { "tipo", "descricao" },
                            properties = new
                            {
                                tipo = new { type = "string", @enum = new[] { "escala", "profissional", "mutirao", "dia", "horario", "unidade", "aproveitamento", "outro" } },
                                descricao = new { type = "string", description = "O que fazer, onde, quanto. Ex.: 'Abrir 2 blocos de 20 vagas na 3ª e 5ª no CDT'." },
                                unidade = new { type = "string" },
                                impactoVagasSemana = new { type = "number", description = "Vagas/semana que a ação acrescenta (estimativa)." },
                            },
                        },
                    },
                    riscos = new { type = "array", items = new { type = "string" } },
                    confianca = new { type = "number", description = "0 a 1." },
                },
            }),
    ];
}
