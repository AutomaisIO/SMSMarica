using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Sinaliza que a conversa deve ir para um atendente humano. O hand-off em si (deixar na fila,
/// não virar dono) é conduzido pela flag <c>handoff</c> do responder_cidadao — aqui apenas
/// confirmamos ao modelo e deixamos trilha.
/// </summary>
public sealed class EncaminharParaHumanoComando : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.EncaminharParaHumano;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:encaminhar";

    public Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct) =>
        Task.FromResult(new RoboComandoResultado(true,
            "Ok. Encerre com uma mensagem curta avisando que um atendente vai continuar, e defina handoff=true."));
}
