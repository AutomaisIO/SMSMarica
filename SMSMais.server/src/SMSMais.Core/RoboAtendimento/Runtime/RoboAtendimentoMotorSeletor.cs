using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Escolhe o motor a cada turno pela configuração (<c>RoboConfiguracao.Motor</c>) — é o que torna a
/// migração para a Messages API reversível <b>sem deploy</b>: se a API der problema no piloto, o
/// operador volta para a assinatura na própria tela e o robô continua atendendo.
///
/// A leitura é por turno de propósito: são poucos por minuto e a troca precisa valer na hora.
/// </summary>
public sealed class RoboAtendimentoMotorSeletor(
    SmsMaisDbContext db,
    RoboAtendimentoMotorApi motorApi,
    RoboAtendimentoMotorHttp motorAssinatura,
    ILogger<RoboAtendimentoMotorSeletor> logger) : IRoboAtendimentoMotor
{
    public async Task<RespostaMotorRobo> ResponderAsync(EntradaMotorRobo entrada, CancellationToken ct)
    {
        var motor = await db.RoboConfiguracoes.AsNoTracking()
            .Select(c => (MotorRobo?)c.Motor)
            .FirstOrDefaultAsync(ct) ?? MotorRobo.Assinatura;

        logger.LogDebug("Robô respondendo pelo motor {Motor} na conversa {Conversa}.", motor, entrada.ConversaId);
        return motor == MotorRobo.Api
            ? await motorApi.ResponderAsync(entrada, ct)
            : await motorAssinatura.ResponderAsync(entrada, ct);
    }
}
