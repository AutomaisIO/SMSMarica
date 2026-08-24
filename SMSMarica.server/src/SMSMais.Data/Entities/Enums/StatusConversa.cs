namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Situação de uma <see cref="Conversas.Conversa"/>. Estados "vivos" (que aparecem nas filas
/// e travam nova conversa para o mesmo contato) são <see cref="Aberta"/> e <see cref="Pendente"/>.
/// O valor inteiro é estável (persistido) — não renumerar.
/// </summary>
public enum StatusConversa
{
    /// <summary>Em atendimento / com resposta do cidadão dentro da janela de 24h.</summary>
    Aberta = 1,

    /// <summary>Template disparado pelo operador, aguardando a primeira resposta do cidadão.</summary>
    Pendente = 2,

    /// <summary>Concluída pelo operador; pode ser reaberta se o cidadão responder.</summary>
    Resolvida = 3,

    /// <summary>Encerrada e arquivada.</summary>
    Fechada = 4,
}
