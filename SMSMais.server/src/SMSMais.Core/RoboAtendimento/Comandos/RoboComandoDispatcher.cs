using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>Executa um comando do robô: valida habilitação por assunto, idempotência e trilha (RoboAcao).</summary>
public interface IRoboComandoDispatcher
{
    Task<RoboComandoResultado> ExecutarAsync(
        Guid conversaId, Guid? pacienteId, Guid? assuntoId, ComandoRobo comando, JsonElement args, CancellationToken ct);
}

public sealed class RoboComandoDispatcher(
    SmsMaisDbContext db,
    IEnumerable<IRoboComando> comandos,
    ILogger<RoboComandoDispatcher> logger) : IRoboComandoDispatcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoboComandoResultado> ExecutarAsync(
        Guid conversaId, Guid? pacienteId, Guid? assuntoId, ComandoRobo comando, JsonElement args, CancellationToken ct)
    {
        // 1. Habilitação por assunto (defesa em profundidade — a tool só existe na sessão se habilitada).
        if (assuntoId is not { } aid ||
            !await db.RoboAssuntoComandos.AsNoTracking()
                .AnyAsync(c => c.RoboAssuntoId == aid && c.Comando == comando && c.Habilitado, ct))
        {
            return new RoboComandoResultado(false, "Comando não habilitado para este assunto.");
        }

        var handler = comandos.FirstOrDefault(c => c.Comando == comando);
        if (handler is null)
        {
            logger.LogWarning("Comando {Comando} sem handler registrado.", comando);
            return new RoboComandoResultado(false, "Comando indisponível.");
        }

        var telefone = await db.Conversas.AsNoTracking()
            .Where(c => c.Id == conversaId).Select(c => c.TelefoneCanonical).FirstOrDefaultAsync(ct) ?? string.Empty;

        var ctx = new RoboComandoContexto(conversaId, pacienteId, assuntoId, telefone, args);

        // 2. Idempotência: se já houve uma ação bem-sucedida com a mesma chave, não reexecuta.
        var chave = handler.Idempotente
            ? handler.ChaveIdempotencia(ctx)
            : $"{conversaId}:{comando}:{Guid.CreateVersion7()}";

        if (handler.Idempotente)
        {
            var previa = await db.RoboAcoes.AsNoTracking()
                .FirstOrDefaultAsync(a => a.IdempotenciaChave == chave && a.Sucesso, ct);
            if (previa is not null)
                return new RoboComandoResultado(true, "Já registrado anteriormente.", null);
        }

        // 3. Execução.
        RoboComandoResultado resultado;
        try
        {
            resultado = await handler.ExecutarAsync(ctx, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Comando {Comando} falhou na conversa {Conversa}.", comando, conversaId);
            resultado = new RoboComandoResultado(false, "Não foi possível executar agora.");
        }

        // 4. Trilha (RoboAcao). Best-effort — não derruba a resposta ao modelo se a gravação falhar.
        try
        {
            db.RoboAcoes.Add(new RoboAcao
            {
                Id = Guid.CreateVersion7(),
                ConversaId = conversaId,
                RoboAssuntoId = assuntoId,
                Comando = comando,
                EntradaJson = args.ValueKind == JsonValueKind.Undefined ? null : args.GetRawText(),
                ResultadoJson = resultado.Dados is null ? null : JsonSerializer.Serialize(resultado.Dados, Json),
                Sucesso = resultado.Sucesso,
                IdempotenciaChave = chave,
                OcorridoEm = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Não foi possível gravar a trilha do comando {Comando}.", comando);
        }

        return resultado;
    }
}
