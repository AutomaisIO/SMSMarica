namespace SMSMarica.Data.Entities;

/// <summary>
/// Resultado de uma execução do indicador para um período. Guardado para (a) servir de prova
/// datada do número apurado e (b) evitar reexecutar consulta pesada no Oracle a cada abertura
/// da tela. Falha também é gravada (com <see cref="Erro"/>) — execução que quebrou é informação.
/// </summary>
public class IndicadorExecucao
{
    public Guid Id { get; set; }

    public Guid IndicadorId { get; set; }
    public Indicador? Indicador { get; set; }

    /// <summary>Versão do SQL que produziu este número — sem isso o resultado não é reproduzível.</summary>
    public Guid? IndicadorVersaoId { get; set; }

    /// <summary>Código do hospital na base de origem (Salux: 1 = HMCML).</summary>
    public int Hospital { get; set; }

    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFim { get; set; }

    public decimal? Numerador { get; set; }
    public decimal? Denominador { get; set; }

    /// <summary>Resultado final já aplicado o tipo (percentual, média, densidade, absoluto).</summary>
    public decimal? Valor { get; set; }

    /// <summary>Linhas da distribuição, em JSON, quando o tipo é Distribuicao.</summary>
    public string? DistribuicaoJson { get; set; }

    /// <summary>
    /// Se o resultado bateu a meta contratual. Nulo quando a meta não é comparável
    /// automaticamente (indicador de distribuição, meta textual sem operador).
    /// </summary>
    public bool? AtingiuMeta { get; set; }

    /// <summary>Pontuação obtida no mês: o peso do indicador se atingiu a meta, senão zero.</summary>
    public decimal? PontuacaoApurada { get; set; }

    public int DuracaoMs { get; set; }
    public string? Erro { get; set; }

    public DateTime ExecutadoEm { get; set; }
    public Guid? ExecutadoPor { get; set; }
}
