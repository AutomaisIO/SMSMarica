using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Data.Entities.EstrategiasFila;

/// <summary>
/// Uma <b>estratégia de fila</b>: o plano de mudança na oferta de UM procedimento, simulado
/// contra a fila real e guardado para consulta (ADR-0058).
///
/// <para><b>O que é e o que não é.</b> É a "cabeça" do planejamento: nome, procedimento, estado e
/// os parâmetros vigentes (com as travas). O conteúdo de cada execução — cenário no momento,
/// projeção, proposta do agente — fica em <see cref="EstrategiaFilaRodada"/>, append-only, para
/// que "rodar de novo" nunca apague o que se viu antes. <b>Nada aqui escreve no SISREG nem em
/// sistema externo nenhum</b>: quem executa a estratégia é gente, por fora; o sistema só guarda o
/// plano e, se avisado, a data em que foi aplicado.</para>
///
/// <para><b>Eixo do procedimento.</b> Código do SISREG (o <c>pa</c>) + nome, com a família
/// grupo↔item resolvida na hora da leitura. O código é nulo quando o procedimento só existe na
/// fila (pediram, ninguém oferta) — caso que precisa aparecer, não sumir por falta de escala.</para>
/// </summary>
public sealed class EstrategiaFila
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Código do procedimento no SISREG. Nulo = procedimento só da fila, sem escala.</summary>
    public string? ProcedimentoCodigo { get; set; }

    /// <summary>Nome do procedimento como o SISREG o escreve — é a chave da fila.</summary>
    public string ProcedimentoNome { get; set; } = string.Empty;

    /// <summary>Canônico do catálogo (ADR-0055), quando o pareamento existe. Só para exibir.</summary>
    public Guid? RegulacaoProcedimentoId { get; set; }
    public RegulacaoProcedimento? RegulacaoProcedimento { get; set; }

    public StatusEstrategiaFila Status { get; set; } = StatusEstrategiaFila.Rascunho;

    /// <summary>Parâmetros vigentes, com trava por campo (JSON de <c>ParametrosEstrategia</c>).</summary>
    public string ParametrosJson { get; set; } = "{}";

    /// <summary>A rodada que representa a estratégia hoje (a última bem-sucedida).</summary>
    public Guid? RodadaAtualId { get; set; }

    public DateTime? AplicadaEm { get; set; }
    public Guid? AplicadaPor { get; set; }
    public string? AplicacaoNota { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public ICollection<EstrategiaFilaRodada> Rodadas { get; set; } = [];
}
