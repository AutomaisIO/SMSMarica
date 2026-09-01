using SMSMais.Data.Entities.Enums;

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

    /// <summary>Motor que responde pelo robô. Default <see cref="MotorRobo.Assinatura"/> (o caminho
    /// antigo) para que o deploy da migração não mude comportamento nenhum: virar para
    /// <see cref="MotorRobo.Api"/> é uma decisão explícita na tela, e voltar também.</summary>
    public MotorRobo Motor { get; set; } = MotorRobo.Assinatura;

    /// <summary>Nome de exibição do robô nas conversas (ex.: "Assistente virtual").</summary>
    public string NomeExibicao { get; set; } = "Assistente virtual";

    /// <summary>Mensagem enviada ao cidadão ao devolver a conversa para um humano.</summary>
    public string? MensagemHandOff { get; set; }

    /// <summary>Mensagem quando fora do horário de atendimento e o robô não resolveu.</summary>
    public string? MensagemForaHorario { get; set; }

    /// <summary>Início do expediente dos ATENDENTES humanos (Brasília, ex.: 08:00). ANTES disso o
    /// robô assume mesmo com humano na sessão (os atendentes ainda não chegaram). <c>null</c> = sem
    /// limite de manhã.</summary>
    public TimeOnly? HoraAtendimentoHumanoInicio { get; set; }

    /// <summary>Fim do expediente dos ATENDENTES humanos (Brasília, ex.: 17:10). A PARTIR desse
    /// horário o robô assume mesmo com humano na sessão aberta (os atendentes já saíram).
    /// <c>null</c> = sem limite de fim. Ambos nulos ⇒ a trava humano-por-janela vale o dia todo.</summary>
    public TimeOnly? HoraAtendimentoHumanoFim { get; set; }

    /// <summary>Dias da semana COM atendente humano (bitmask, bit 0 = domingo — mesma convenção de
    /// <see cref="RoboAssunto.DiasSemana"/>). Nulo = todos os dias. Padrão: segunda a sexta (62) —
    /// sem isto, sábado de manhã contava como "atendente disponível" e o robô prometia atendente
    /// que só chega segunda.</summary>
    public int? DiasSemanaAtendimentoHumano { get; set; } = 62;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
