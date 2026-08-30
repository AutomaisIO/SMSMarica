using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Um "assunto" que o robô de atendimento sabe tratar no WhatsApp (ex.: "Confirmação de
/// agendamento", "Número errado", "Dúvidas de exame"). Não é um bot "pra tudo": cada assunto
/// carrega a própria persona, seus <see cref="Treinos"/> (material que o operador vai
/// refinando), as <see cref="Condicoes"/> de ativação (classificação barata por texto) e os
/// <see cref="Comandos"/> liberados — de forma que o robô não consiga sair do que aquele
/// assunto permite. Ver o plano do Robô por Assunto (ADR pendente).
/// </summary>
public class RoboAssunto
{
    public Guid Id { get; set; }

    /// <summary>Nome do assunto (único entre os ativos).</summary>
    public string Nome { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    /// <summary>Instruções/persona injetadas no prompt do assunto (system).</summary>
    public string InstrucoesPersona { get; set; } = string.Empty;

    /// <summary>Modelo de IA a usar neste assunto (ex.: "claude-haiku-4-5-20251001").
    /// <c>null</c> = usa o <see cref="RoboConfiguracao.ModeloPadrao"/> global.</summary>
    public string? Modelo { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>Início do horário de atendimento (fuso Brasília). <c>null</c> junto com
    /// <see cref="HorarioFim"/> = sem restrição de horário.</summary>
    public TimeOnly? HorarioInicio { get; set; }

    public TimeOnly? HorarioFim { get; set; }

    /// <summary>Dias da semana atendidos, como bitmask (bit 0 = domingo … bit 6 = sábado).
    /// <c>null</c> = todos os dias.</summary>
    public int? DiasSemana { get; set; }

    /// <summary>Quantas interações do robô sem resolver antes de cair para o humano.</summary>
    public int MaxInteracoesSemResolver { get; set; } = 20;

    /// <summary>Confiança mínima (0..1) para o robô agir; abaixo disso, hand-off.</summary>
    public double LimiarConfianca { get; set; } = 0.6;

    /// <summary>Unidade para onde a conversa é encaminhada no hand-off. <c>null</c> = triagem geral.</summary>
    public Guid? EscalonamentoUnidadeId { get; set; }

    /// <summary>Prioridade na classificação por condições (menor primeiro).</summary>
    public int Ordem { get; set; }

    /// <summary>
    /// Assunto usado quando NENHUM outro casa com a mensagem. No máximo um (índice único parcial).
    /// Sem ele, "sem assunto" não é um estado neutro: é o robô sem orientação, sem treinos, sem
    /// limiar de confiança e sem os comandos do assunto — e 22% das mensagens caem aí.
    /// </summary>
    public bool Padrao { get; set; }

    /// <summary>Token de concorrência (xmin do Postgres).</summary>
    public uint RowVersion { get; set; }

    public ICollection<RoboAssuntoCondicao> Condicoes { get; set; } = [];
    public ICollection<RoboAssuntoTreino> Treinos { get; set; } = [];
    public ICollection<RoboAssuntoComando> Comandos { get; set; } = [];

    public Unidade? EscalonamentoUnidade { get; set; }

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}

/// <summary>
/// Condição de ativação de um <see cref="RoboAssunto"/> — o pré-match barato (palavra-chave /
/// regex / frase) que roteia a mensagem do cidadão ao assunto antes de chamar a IA.
/// </summary>
public class RoboAssuntoCondicao
{
    public Guid Id { get; set; }

    public Guid RoboAssuntoId { get; set; }

    public TipoCondicaoRobo Tipo { get; set; } = TipoCondicaoRobo.PalavraChave;

    public string Valor { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public int Ordem { get; set; }

    public RoboAssunto? RoboAssunto { get; set; }
}

/// <summary>
/// Um "treino" de um <see cref="RoboAssunto"/> — instrução, exemplo, glossário ou do/don't que
/// o operador insere para personalizar e melhorar o atendimento daquele assunto.
/// </summary>
public class RoboAssuntoTreino
{
    public Guid Id { get; set; }

    public Guid RoboAssuntoId { get; set; }

    public TipoTreinoRobo Tipo { get; set; } = TipoTreinoRobo.Instrucao;

    public string? Titulo { get; set; }

    public string Conteudo { get; set; } = string.Empty;

    public int Ordem { get; set; }

    public bool Ativo { get; set; } = true;

    public RoboAssunto? RoboAssunto { get; set; }
}

/// <summary>
/// Liga/desliga de um <see cref="ComandoRobo"/> para um <see cref="RoboAssunto"/>. Só os
/// comandos com <see cref="Habilitado"/> viram ferramenta na sessão do assunto — é o que
/// impede o robô de "furar" o conjunto de comandos permitidos.
/// </summary>
public class RoboAssuntoComando
{
    public Guid Id { get; set; }

    public Guid RoboAssuntoId { get; set; }

    public ComandoRobo Comando { get; set; }

    public bool Habilitado { get; set; }

    public RoboAssunto? RoboAssunto { get; set; }
}
