using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Erros;
using SMSMais.Core.Erros.Dtos;

namespace SMSMais.Api.Middleware;

public sealed partial class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;
    private readonly bool _detailedErrors =
        environment.IsDevelopment()
        || configuration.GetValue("DetailedErrors", defaultValue: false);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NaoEncontradoException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status404NotFound, "Não encontrado", ex.Message);
        }
        catch (ConflitoException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito", ex.Message, type: ex.Codigo);
        }
        catch (ValidacaoException ex)
        {
            await EscreverValidationProblem(context, ex.Erros);
        }
        // Corrida perdida: violação do índice único (ex.: 2ª assinatura concluída do
        // mesmo laudo) ou conflito otimista (xmin) viram 409 limpo, não 500.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito",
                "O recurso já foi processado por outra requisição concorrente.",
                type: "concorrencia.violacao_unica");
        }
        // O 409 saía SEM log nenhum: nem tabela, nem linha, nem estado. E "outra requisição
        // concorrente" é só o rótulo — a causa real costuma ser gravação que não achou a linha
        // que esperava (0 linhas afetadas), inclusive com ninguém mais usando o sistema.
        catch (DbUpdateConcurrencyException ex)
        {
            LogConflitoConcorrencia(_logger, ex, context.Request.Path, DescreverEntradas(ex), context.TraceIdentifier);
            await EscreverProblemDetails(context, StatusCodes.Status409Conflict, "Conflito",
                "O recurso foi alterado por outra requisição concorrente. Tente novamente.",
                type: "concorrencia.token");
        }
        catch (UnauthorizedAccessException ex)
        {
            await EscreverProblemDetails(context, StatusCodes.Status403Forbidden, "Acesso negado", ex.Message);
        }
        // Falha de armazenamento (S3/Spaces): NÃO há fallback local — alerta o usuário a
        // procurar o suporte. A mensagem é exposta (503) e o erro é logado (infra).
        catch (ArmazenamentoIndisponivelException ex)
        {
            LogErroNaoTratado(_logger, ex, context.Request.Path);
            var reg = await PersistirErroEObterCodigo(context, ex, StatusCodes.Status503ServiceUnavailable);
            await EscreverProblemDetails(context, StatusCodes.Status503ServiceUnavailable,
                "Armazenamento indisponível", $"{ex.Message} (código {reg.Codigo})",
                type: ex.Codigo, codigoReferencia: reg.Codigo, jaReportado: reg.JaReportado);
        }
        // Cliente abortou a requisição (fechou a aba, viewer PACS cancelou o download de
        // frames): não é falha do sistema — não registra em registro_erro nem tenta
        // responder (a conexão já foi embora). Timeout interno com cliente ainda
        // conectado NÃO cai aqui e segue sendo registrado como erro real.
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogErroNaoTratado(_logger, ex, context.Request.Path);
            var reg = await PersistirErroEObterCodigo(context, ex, StatusCodes.Status500InternalServerError);
            await EscreverProblemDetails(
                context,
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                $"Ocorreu um erro inesperado. Informe o código {reg.Codigo} ao suporte para que possamos resolver.",
                type: "erro.nao_tratado",
                excecao: _detailedErrors ? ex : null,
                codigoReferencia: reg.Codigo,
                jaReportado: reg.JaReportado);
        }
    }

    /// <summary>
    /// Persiste o erro no log do banco (best-effort) e devolve o código de referência.
    /// Se a própria gravação falhar (ex.: o DB caiu), NÃO mascara o erro original:
    /// loga e devolve um código derivado do TraceId para o usuário ainda ter o que reportar.
    /// </summary>
    private async Task<RegistroErroResultado> PersistirErroEObterCodigo(HttpContext context, Exception ex, int statusCode)
    {
        try
        {
            var servico = context.RequestServices.GetRequiredService<IRegistroErroService>();
            var ua = context.Request.Headers.UserAgent.ToString();
            var dados = new RegistrarErroDados(
                Metodo: context.Request.Method,
                Caminho: context.Request.Path.Value ?? string.Empty,
                QueryString: context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                StatusCode: statusCode,
                TipoExcecao: ex.GetType().FullName ?? ex.GetType().Name,
                Mensagem: ex.Message,
                StackTrace: ex.StackTrace,
                Interna: ex.InnerException is null
                    ? null
                    : $"{ex.InnerException.GetType().FullName}: {ex.InnerException.Message}",
                TraceId: context.TraceIdentifier,
                UserAgent: string.IsNullOrEmpty(ua) ? null : ua);

            // CancellationToken.None: garante que o log seja gravado mesmo se o cliente desistir.
            return await servico.RegistrarAsync(dados, CancellationToken.None);
        }
        catch (Exception persistEx)
        {
            _logger.LogError(persistEx,
                "Falha ao persistir RegistroErro (erro original em {Path}). TraceId={TraceId}",
                context.Request.Path, context.TraceIdentifier);
            return new RegistroErroResultado($"ERRO-{context.TraceIdentifier}", JaReportado: false, 1);
        }
    }

    private static Task EscreverProblemDetails(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? type = null,
        Exception? excecao = null,
        string? codigoReferencia = null,
        bool jaReportado = false)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path,
        };

        if (codigoReferencia is not null)
            problem.Extensions["codigoReferencia"] = codigoReferencia;

        if (jaReportado)
            problem.Extensions["jaReportado"] = true;

        if (excecao is not null)
        {
            problem.Extensions["exception"] = new
            {
                type = excecao.GetType().FullName,
                message = excecao.Message,
                inner = excecao.InnerException is null
                    ? null
                    : $"{excecao.InnerException.GetType().FullName}: {excecao.InnerException.Message}",
                stackTrace = excecao.StackTrace,
            };
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static Task EscreverValidationProblem(HttpContext context, IDictionary<string, string[]> erros)
    {
        var problem = new ValidationProblemDetails(erros)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validação inválida",
            Instance = context.Request.Path,
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado em {Path}")]
    private static partial void LogErroNaoTratado(ILogger logger, Exception ex, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "CONFLITO_CONCORRENCIA em {Path}: {Entradas} (TraceId={TraceId})")]
    private static partial void LogConflitoConcorrencia(
        ILogger logger, Exception ex, string path, string entradas, string traceId);

    /// <summary>
    /// Quem eram as linhas que a gravação esperava encontrar. É a única pista que separa
    /// "outro operador editou junto" de "a linha sumiu embaixo da gravação" — sem ela, o 409
    /// é indiagnosticável depois do fato.
    /// </summary>
    private static string DescreverEntradas(DbUpdateConcurrencyException ex)
    {
        if (ex.Entries.Count == 0) return "(a exceção não trouxe entradas)";

        return string.Join(" | ", ex.Entries.Select(entrada =>
        {
            var tabela = entrada.Metadata.GetTableName() ?? entrada.Metadata.Name;
            var chave = entrada.Metadata.FindPrimaryKey();
            var valores = chave is null
                ? "?"
                : string.Join(',', chave.Properties.Select(p => entrada.Property(p.Name).CurrentValue));
            return $"{tabela}[{valores}] {entrada.State}";
        }));
    }
}
