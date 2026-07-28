using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Indicador contratual da planilha do HMCML. O "motor" do indicador é o próprio SQL
/// (<see cref="Sql"/>), guardado aqui e editável na tela — cada gravação vira uma
/// <see cref="IndicadorVersao"/>, então dá para acompanhar e reverter o aprimoramento.
///
/// A execução é sempre read-only: o SQL passa pelo guard antes de ir para a fonte
/// (<see cref="IaFonte"/>, hoje o Oracle do Salux). Ver ADR-0022.
///
/// Contrato do SQL, por <see cref="TipoResultado"/>:
/// <list type="bullet">
///   <item>Razao/Densidade — 1 linha com as colunas <c>numerador</c> e <c>denominador</c>.</item>
///   <item>Absoluto — 1 linha com a coluna <c>numerador</c>.</item>
///   <item>Distribuicao — N linhas com as colunas <c>rotulo</c> e <c>quantidade</c>.</item>
///   <item>Agrupador — sem SQL; soma os filhos.</item>
/// </list>
/// Parâmetros disponíveis (nomeados, nunca concatenados): <c>:ini</c>, <c>:fim</c>, <c>:hospital</c>.
/// </summary>
public class Indicador
{
    public Guid Id { get; set; }

    public AbaIndicador Aba { get; set; }

    /// <summary>Número como aparece na planilha ("1", "3.1", "10.4"). É rótulo, não ordenação.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Ordenação estável dentro da aba (a planilha mistura 3, 3.1, 4…).</summary>
    public int Ordem { get; set; }

    /// <summary>
    /// Indicador agrupador ao qual este pertence (3.1 aponta para 3). Na planilha o pai é
    /// <c>=SUM(filhos)</c> e não tem cálculo próprio.
    /// </summary>
    public Guid? IndicadorPaiId { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Memória de cálculo como está na planilha — o que foi pactuado com a SMS.</summary>
    public string? MemoriaCalculo { get; set; }

    /// <summary>Fonte declarada na planilha ("PEP ou SIH", "Relatório do NSP"…).</summary>
    public string? FonteDeclarada { get; set; }

    /// <summary>Meta contratual como texto, exatamente como está na planilha ("≤ 8 h", "≥ 85%").</summary>
    public string? Meta { get; set; }

    /// <summary>Como comparar o resultado com a meta. Nulo = não dá para pontuar automaticamente.</summary>
    public MetaOperador? MetaOperador { get; set; }

    /// <summary>Valor numérico da meta, na mesma unidade do resultado.</summary>
    public decimal? MetaValor { get; set; }

    /// <summary>Limite superior quando <see cref="MetaOperador"/> é <c>Entre</c>.</summary>
    public decimal? MetaValorMaximo { get; set; }

    /// <summary>Pontuação que o indicador vale no contrato (o peso máximo do mês).</summary>
    public decimal? Pontuacao { get; set; }

    public TipoResultadoIndicador TipoResultado { get; set; }

    /// <summary>Unidade do resultado, para formatar ("min", "h", "dias", "%").</summary>
    public string? UnidadeMedida { get; set; }

    /// <summary>Multiplicador da densidade (1.000 em "por 1.000 pacientes-dia").</summary>
    public decimal? FatorDensidade { get; set; }

    public SituacaoIndicador Situacao { get; set; } = SituacaoIndicador.SemMotor;

    /// <summary>Base onde o SQL roda. Nulo enquanto não há motor.</summary>
    public Guid? FonteId { get; set; }
    public IaFonte? Fonte { get; set; }

    /// <summary>O motor. Nulo quando <see cref="Situacao"/> é SemMotor/ForaDoBanco.</summary>
    public string? Sql { get; set; }

    /// <summary>
    /// SQL do relatório analítico — a evidência linha a linha por trás do número. Opcional e
    /// independente do motor: o <see cref="Sql"/> continua devolvendo só o agregado (é ele que
    /// pontua), e este devolve os registros que o compõem, para auditoria.
    ///
    /// Contrato: N linhas, colunas livres (atendimento, paciente, data…) mais duas obrigatórias:
    /// <list type="bullet">
    ///   <item><c>incluido</c> — <c>'S'</c> se o registro entrou na conta, <c>'N'</c> se foi excluído.</item>
    ///   <item><c>motivo_exclusao</c> — por que saiu (nulo quando <c>incluido = 'S'</c>).</item>
    /// </list>
    /// Ou seja: o analítico <b>classifica</b> em vez de filtrar — sem isso não há como provar o
    /// que ficou de fora. Mesmos parâmetros do motor: <c>:ini</c>, <c>:fim</c>, <c>:hospital</c>.
    /// Roda sob demanda na exportação; nada é persistido.
    /// </summary>
    public string? SqlAnalitico { get; set; }

    /// <summary>
    /// Ressalva honesta exibida junto do número: cobertura do campo, definição pendente,
    /// divergência com a planilha. Nunca esconder limitação atrás de um número bonito.
    /// </summary>
    public string? Ressalva { get; set; }

    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public ICollection<IndicadorVersao> Versoes { get; set; } = [];
}
