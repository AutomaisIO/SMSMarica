using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Trilha append-only das AÇÕES REAIS executadas pelo robô (v1: confirmar presença, iniciar
/// cancelamento, registrar número errado, etc.). Cada linha é a prova auditável de um comando,
/// e a <see cref="IdempotenciaChave"/> única impede que a mesma ação seja aplicada duas vezes.
/// </summary>
public class RoboAcao
{
    public Guid Id { get; set; }

    public Guid ConversaId { get; set; }

    public Guid? RoboAssuntoId { get; set; }

    public ComandoRobo Comando { get; set; }

    /// <summary>Argumentos recebidos do modelo (jsonb).</summary>
    public string? EntradaJson { get; set; }

    /// <summary>Resultado devolvido pelo handler do comando (jsonb).</summary>
    public string? ResultadoJson { get; set; }

    public bool Sucesso { get; set; }

    /// <summary>Chave de idempotência (ex.: <c>{conversaId}:{comando}</c>) — única.</summary>
    public string IdempotenciaChave { get; set; } = string.Empty;

    public DateTime OcorridoEm { get; set; }

    public Conversa? Conversa { get; set; }
    public RoboAssunto? RoboAssunto { get; set; }
}
