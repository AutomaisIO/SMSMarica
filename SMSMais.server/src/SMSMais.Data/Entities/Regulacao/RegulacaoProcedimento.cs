using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Procedimento canônico — o que o solicitante vê como "um" procedimento na busca, reunindo as
/// origens equivalentes dos sistemas de regulação (ADR-0052).
///
/// <para><b>O catálogo é plano, sem hierarquia</b> (D-10). Quando o SERNIT oferece
/// <c>Endocrinologia</c> como balde único e o SER quebra o mesmo assunto em
/// <c>ENDOCRINOLOGIA - ANDROLOGIA</c>, <c>- DIABETES</c> etc., cada um é um canônico
/// <b>distinto</b> e a busca devolve todos. Quem desempata é o agente regulador, que pode trocar
/// o procedimento na triagem. Medido no spike c: são 17 casos assim, e forçá-los num só canônico
/// obrigaria o solicitante a escolher uma subespecialidade que ele não tem como saber.</para>
///
/// <para><b>Só fundem em um canônico as origens que casam por igualdade de chave</b> — rótulo
/// normalizado em conjunto de tokens mais a dimensão público (spike c §1). Contenção
/// (<c>Endocrinologia</c> ⊃ <c>Endocrinologia - Diabetes</c>) <b>nunca</b> funde: vira sugestão
/// para a curadoria, porque dois dos candidatos por contenção medidos eram falsos.</para>
/// </summary>
public sealed class RegulacaoProcedimento
{
    public Guid Id { get; set; }

    /// <summary>Nome exibido. Nasce do rótulo da primeira origem e é editável na curadoria.</summary>
    public string NomeCanonico { get; set; } = string.Empty;

    /// <summary>Nome sem acento/pontuação, usado na busca lexical.</summary>
    public string NomeNormalizado { get; set; } = string.Empty;

    public TipoProcedimentoRegulacao Tipo { get; set; }

    /// <summary>
    /// SIGTAP correlato, quando conhecido. <b>Opcional de propósito</b>: o eixo do catálogo é o
    /// nome do procedimento no sistema de origem, não o SIGTAP — o SIGTAP exportado pelo SISREG
    /// é defasado e amarrar o canônico a ele deixaria de fora o que o SER e o SERNIT oferecem.
    /// </summary>
    public Guid? ProcedimentoSigtapId { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }

    public ICollection<RegulacaoProcedimentoOrigem> Origens { get; set; } = [];
}
