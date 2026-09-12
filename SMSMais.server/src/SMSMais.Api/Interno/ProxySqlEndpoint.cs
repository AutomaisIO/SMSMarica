using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Identidade;
using SMSMais.Core.Inteligencia.Fontes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Api.Interno;

/// <summary>
/// Proxy SQL interno: executa consultas de LEITURA contra qualquer base já cadastrada
/// (<c>ia_fonte</c>), alcançada do jeito que aquela base exige — Oracle direto pelo túnel ou
/// SQL Server pelo agente WSS reverso. Quem chama não precisa saber o dialeto nem guardar
/// credencial: manda o slug da base e o SQL.
///
/// Existe para que serviços da própria máquina (hoje o painel do Secretário) parem de carregar
/// credenciais de banco e parem de duplicar drivers. O smsmarica vira o único lugar que sabe
/// alcançar cada base.
///
/// SEGURANÇA — duas camadas, porque este endpoint NÃO usa o JWT de usuário:
///
/// 1. <b>Porta dedicada de loopback.</b> Só responde quando a requisição chegou pela porta
///    interna (<c>ProxySql:Porta</c>), que o Kestrel atende apenas em 127.0.0.1 e o nginx nunca
///    encaminha. Na porta pública (5080) o endpoint responde 404 — nem existe.
///    Deliberadamente NÃO confiamos em "IP de origem é loopback": o nginx faz proxy_pass para
///    127.0.0.1, então essa checagem dependeria do X-Forwarded-For continuar correto. Porta
///    física não depende de configuração de header.
/// 2. <b>Token compartilhado</b> (<c>ProxySql:Token</c>), comparado em tempo constante.
///
/// Read-only continua garantido pelo <c>SqlReadOnlyGuard</c> dentro de cada <see cref="IFonteDados"/>
/// (e, nas bases via agente, de novo no agente). Ver ADR-0023.
///
/// <para><b>Auditoria.</b> A Consulta Inteligente manda junto quem perguntou e o quê
/// (<see cref="ProxySqlAuditoria"/>); cada SQL vira uma linha em <c>ia_consulta</c>. Nas bases do
/// próprio SMSMais (Regulação, Atendimento — <see cref="PermissaoDaFonte.AuditoriaObrigatoria"/>)
/// isso é obrigatório: sem operador identificado, ou se a linha de auditoria não puder ser gravada,
/// a consulta <b>não roda</b>. E a base Atendimento confere a permissão do operador aqui também —
/// a sessão foi aberta com a permissão, mas perfil muda.</para>
/// </summary>
public static class ProxySqlEndpoint
{
    public static void MapProxySql(this WebApplication app)
    {
        app.MapPost("/proxy-sql", ExecutarAsync).AllowAnonymous();
    }

    private static async Task<IResult> ExecutarAsync(
        ProxySqlRequisicao requisicao,
        HttpContext ctx,
        IOptions<ProxySqlOpcoes> opcoes,
        SmsMaisDbContext db,
        IFonteDadosFactory factory,
        IIdentidadeService identidade,
        ILoggerFactory logs,
        CancellationToken ct)
    {
        var cfg = opcoes.Value;
        var log = logs.CreateLogger("ProxySql");

        // 1. Porta interna. Fora dela o endpoint não existe (404, não 403 — não anuncia).
        if (cfg.Porta <= 0 || ctx.Connection.LocalPort != cfg.Porta)
        {
            return Results.NotFound();
        }

        // 2. Token. Sem token configurado o proxy fica desligado (não abre por omissão).
        if (string.IsNullOrWhiteSpace(cfg.Token))
        {
            log.LogWarning("Proxy SQL chamado mas ProxySql:Token não está configurado — recusado.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var apresentado = ctx.Request.Headers["X-Proxy-Token"].ToString();
        if (!TokenConfere(apresentado, cfg.Token))
        {
            log.LogWarning("Proxy SQL: token inválido.");
            return Results.Unauthorized();
        }

        // 3. Requisição.
        if (string.IsNullOrWhiteSpace(requisicao.Base))
        {
            return Results.BadRequest(new { mensagem = "Informe 'base' (slug da fonte cadastrada)." });
        }

        var consultas = requisicao.Consultas ?? [];
        if (consultas.Count == 0)
        {
            return Results.BadRequest(new { mensagem = "Informe ao menos uma consulta em 'consultas'." });
        }

        if (consultas.Count > cfg.MaxConsultasPorChamada)
        {
            return Results.BadRequest(new
            {
                mensagem = $"Máximo de {cfg.MaxConsultasPorChamada} consultas por chamada (recebidas {consultas.Count}).",
            });
        }

        var slug = requisicao.Base.Trim().ToLowerInvariant();
        var fonte = await db.IaFontes
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Slug == slug && f.Ativo && f.ExcluidoEm == null, ct);

        if (fonte is null)
        {
            return Results.NotFound(new { mensagem = $"Base '{slug}' não encontrada (ou inativa)." });
        }

        var maxLinhas = requisicao.MaxLinhas is > 0
            ? Math.Min(requisicao.MaxLinhas.Value, cfg.MaxLinhasTeto)
            : (int?)null;

        // 4. Quem pergunta. Bases do próprio SMSMais não respondem a chamada anônima, e a base
        //    Atendimento confere a permissão do operador a cada chamada.
        var usuarioId = Guid.TryParse(requisicao.Auditoria?.UsuarioId, out var u) ? u : (Guid?)null;
        var auditoriaObrigatoria = PermissaoDaFonte.AuditoriaObrigatoria(fonte.Tipo);
        if (auditoriaObrigatoria && usuarioId is null)
        {
            log.LogWarning("Proxy SQL [{Base}]: chamada sem operador identificado — recusada.", slug);
            return Results.Json(new
            {
                mensagem = "Esta base só responde a perguntas de um operador identificado (auditoria obrigatória).",
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (!await PermissaoDaFonte.PodeUsarAsync(identidade, usuarioId, fonte.Tipo, ct))
        {
            log.LogWarning("Proxy SQL [{Base}]: usuário {Usuario} sem permissão para a base.", slug, usuarioId);
            return Results.Json(new { mensagem = "O operador não tem permissão para consultar esta base." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var auditar = auditoriaObrigatoria || usuarioId is not null;
        var pergunta = string.IsNullOrWhiteSpace(requisicao.Auditoria?.Pergunta)
            ? "(sem pergunta informada)"
            : requisicao.Auditoria!.Pergunta!.Trim();

        // 5. Execução SEQUENCIAL — são bancos de produção de hospital, não abrimos várias sessões.
        var dados = factory.Criar(fonte);
        var resultados = new List<ProxySqlResultado>(consultas.Count);

        for (var i = 0; i < consultas.Count; i++)
        {
            // A linha de auditoria nasce ANTES de executar: se ela não grava, a consulta não roda.
            IaConsulta? registro = null;
            if (auditar)
            {
                registro = new IaConsulta
                {
                    Id = Guid.CreateVersion7(),
                    FonteId = fonte.Id,
                    Pergunta = pergunta,
                    SqlGerado = consultas[i],
                    Status = StatusConsulta.Executando,
                    Tentativas = 1,
                    CriadoEm = DateTime.UtcNow,
                    CriadoPor = usuarioId,
                };
                db.IaConsultas.Add(registro);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Proxy SQL [{Base}]: auditoria não gravou.", slug);
                    if (auditoriaObrigatoria)
                    {
                        return Results.Json(new { mensagem = "Auditoria indisponível — a consulta não foi executada." },
                            statusCode: StatusCodes.Status503ServiceUnavailable);
                    }

                    db.Entry(registro).State = EntityState.Detached;
                    registro = null;
                }
            }

            var cronometro = Stopwatch.StartNew();
            ResultadoConsulta r;
            try
            {
                r = await dados.ExecutarAsync(consultas[i], ct, maxLinhas);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.LogWarning("Proxy SQL [{Base}] consulta {Indice} rejeitada: {Erro}", slug, i, ex.Message);
                await FecharAuditoriaAsync(db, registro, ResultadoConsulta.ComErro(ex.Message), cronometro, log);
                return Results.BadRequest(new { indice = i, mensagem = ex.Message });
            }

            await FecharAuditoriaAsync(db, registro, r, cronometro, log);

            if (!r.Sucesso)
            {
                log.LogWarning("Proxy SQL [{Base}] consulta {Indice} falhou: {Erro}", slug, i, r.Erro);
                return Results.BadRequest(new { indice = i, mensagem = r.Erro ?? "falha na consulta" });
            }

            resultados.Add(new ProxySqlResultado(r.Colunas, r.Linhas, cronometro.ElapsedMilliseconds));
        }

        log.LogInformation(
            "Proxy SQL [{Base}]: {Qtd} consulta(s) em {Ms} ms.",
            slug, consultas.Count, resultados.Sum(x => x.DuracaoMs));

        return Results.Ok(new ProxySqlResposta(resultados));
    }

    /// <summary>
    /// Desfecho da consulta na linha de auditoria. Melhor esforço: a linha já existe (com o SQL e
    /// quem perguntou), então perder só o desfecho não justifica esconder o resultado do operador.
    /// </summary>
    private static async Task FecharAuditoriaAsync(
        SmsMaisDbContext db, IaConsulta? registro, ResultadoConsulta r, Stopwatch cronometro, ILogger log)
    {
        if (registro is null)
        {
            return;
        }

        registro.Status = r.Sucesso ? StatusConsulta.Sucesso : StatusConsulta.Erro;
        registro.Erro = r.Sucesso ? null : r.Erro;
        registro.DuracaoMs = (int)Math.Min(cronometro.ElapsedMilliseconds, int.MaxValue);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Proxy SQL: desfecho da auditoria {Id} não gravou.", registro.Id);
        }
    }

    private static bool TokenConfere(string apresentado, string esperado)
    {
        if (string.IsNullOrEmpty(apresentado))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(apresentado);
        var b = Encoding.UTF8.GetBytes(esperado);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

/// <summary>Configuração do proxy (<c>ProxySql</c>). Sem token, o proxy fica desligado.</summary>
public sealed class ProxySqlOpcoes
{
    /// <summary>Porta interna (loopback) em que o endpoint responde. 0 desliga.</summary>
    public int Porta { get; set; }

    public string Token { get; set; } = string.Empty;

    public int MaxLinhasTeto { get; set; } = 5000;

    public int MaxConsultasPorChamada { get; set; } = 25;
}

public sealed record ProxySqlRequisicao(
    string Base, IReadOnlyList<string> Consultas, int? MaxLinhas, ProxySqlAuditoria? Auditoria = null);

/// <summary>
/// Quem perguntou e o quê — enviado pelo motor da Consulta Inteligente a cada SQL. O motor é
/// confiável (loopback + token); o operador foi autenticado pela API ao abrir o turno.
/// </summary>
/// <param name="TurnoId">Só para log/correlação com o motor.</param>
public sealed record ProxySqlAuditoria(string? UsuarioId, string? Pergunta, string? TurnoId);

public sealed record ProxySqlResultado(
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    long DuracaoMs);

public sealed record ProxySqlResposta(IReadOnlyList<ProxySqlResultado> Resultados);
