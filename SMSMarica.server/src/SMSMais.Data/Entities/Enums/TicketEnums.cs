namespace SMSMais.Data.Entities.Enums;

/// <summary>Natureza do ticket de suporte aberto pelo usuário.</summary>
public enum TicketTipo
{
    /// <summary>Reportar um comportamento incorreto do sistema.</summary>
    Bug = 1,

    /// <summary>Solicitar um ajuste/mudança em algo existente.</summary>
    Mudanca = 2,

    /// <summary>Sugerir uma melhoria ou funcionalidade nova.</summary>
    Sugestao = 3,

    /// <summary>Dúvida / pedido de ajuda no uso do sistema.</summary>
    Duvida = 4,
}

/// <summary>
/// Fluxo enxuto do ticket: <c>Aberto → EmAnalise → Concluido | Negado</c>.
/// <c>Concluido</c> e <c>Negado</c> são estados finais e exigem um retorno (feedback/justificativa).
/// </summary>
public enum TicketStatus
{
    Aberto = 1,
    EmAnalise = 2,
    Concluido = 3,
    Negado = 4,
}

/// <summary>Prioridade definida pela equipe (admin) ao triar o ticket.</summary>
public enum TicketPrioridade
{
    Baixa = 1,
    Normal = 2,
    Alta = 3,
}

/// <summary>
/// Regra de visibilidade dos tickets entre usuários — configurável pelo admin global
/// a qualquer momento. O admin sempre vê todos, independentemente deste valor.
/// </summary>
public enum TicketVisibilidade
{
    /// <summary>Cada usuário vê apenas os tickets que abriu.</summary>
    Privado = 1,

    /// <summary>Usuários da mesma unidade veem os tickets uns dos outros.</summary>
    PorUnidade = 2,

    /// <summary>Todos os usuários veem todos os tickets (mural).</summary>
    Publico = 3,
}
