namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Configuração global do robô de atendimento (linha única). Liga/desliga geral, modelo padrão
/// e as mensagens fixas de hand-off e de fora-de-horário. Gerida pela tela do módulo Robô.
/// </summary>
public class RoboConfiguracao
{
    /// <summary>PK fixa — a tabela tem sempre uma única linha.</summary>
    public static readonly Guid IdSingleton = new("0b0b0b0b-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>
    /// Persona base aplicada a TODOS os assuntos (é um atendente virtual; não anuncia isso por
    /// conta própria, mas é honesto se perguntarem). Combinada com a persona de cada assunto.
    /// </summary>
    public const string PersonaGlobalPadrao =
        "Você é um atendente virtual da Secretaria de Saúde, atendendo cidadãos pelo WhatsApp. " +
        "Aja com cordialidade e objetividade, como um atendimento humano. NÃO anuncie que é um robô " +
        "ou assistente virtual por conta própria. SE a pessoa perguntar se você é um robô, atendente " +
        "virtual, sistema ou pessoa, seja honesto e diga que é um assistente virtual da Secretaria de " +
        "Saúde. Nunca invente informações; quando não souber ou o assunto fugir do que pode tratar, " +
        "encaminhe para um atendente humano. Nunca peça senha nem dados sensíveis além do necessário.";

    /// <summary>Interruptor geral do robô. Desligado, nenhuma conversa é atendida automaticamente.</summary>
    public bool Ativo { get; set; }

    /// <summary>Persona base do robô (comum a todos os assuntos). Ver <see cref="PersonaGlobalPadrao"/>.</summary>
    public string PersonaGlobal { get; set; } = PersonaGlobalPadrao;

    /// <summary>Modelo padrão quando o assunto não define o seu (default Haiku).</summary>
    public string ModeloPadrao { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Nome de exibição do robô nas conversas (ex.: "Assistente virtual").</summary>
    public string NomeExibicao { get; set; } = "Assistente virtual";

    /// <summary>Mensagem enviada ao cidadão ao devolver a conversa para um humano.</summary>
    public string? MensagemHandOff { get; set; }

    /// <summary>Mensagem quando fora do horário de atendimento e o robô não resolveu.</summary>
    public string? MensagemForaHorario { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
