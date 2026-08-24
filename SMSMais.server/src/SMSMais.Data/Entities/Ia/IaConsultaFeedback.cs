using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ia;

/// <summary>
/// Avaliação de uma resposta da Consulta Inteligente (botão 👍/👎). Um 👎 vira item pendente na
/// tela de Melhorias de IA, para o admin enriquecer o conhecimento (.md) da base. A conversa
/// vive no motor (SQLite), então guardamos aqui o SNAPSHOT (pergunta + resposta) no momento da
/// avaliação. <see cref="Familia"/> é denormalizada para agrupar bases de mesma estrutura
/// (ex.: UPA e Santa Rita = klinikos) — o aprendizado de uma vale para a família. Ver ADR-0023.
/// </summary>
public class IaConsultaFeedback
{
    public Guid Id { get; set; }

    public Guid FonteId { get; set; }
    public IaFonte? Fonte { get; set; }

    /// <summary>Família da base no momento da avaliação (denormalizada; ver <see cref="IaFonte.Familia"/>).</summary>
    public string? Familia { get; set; }

    public string Pergunta { get; set; } = string.Empty;

    /// <summary>Snapshot do que o assistente respondeu (texto final mostrado ao operador).</summary>
    public string? Resposta { get; set; }

    /// <summary>true = útil (👍); false = não resolveu (👎, é o que gera melhoria).</summary>
    public bool Util { get; set; }

    /// <summary>Comentário opcional do operador ("o que faltou?").</summary>
    public string? Comentario { get; set; }

    public StatusFeedbackIa Status { get; set; } = StatusFeedbackIa.Pendente;

    /// <summary>Observação de quem tratou (o que foi enriquecido / por que foi descartado).</summary>
    public string? Resolucao { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? TratadoEm { get; set; }
    public Guid? TratadoPor { get; set; }
}
