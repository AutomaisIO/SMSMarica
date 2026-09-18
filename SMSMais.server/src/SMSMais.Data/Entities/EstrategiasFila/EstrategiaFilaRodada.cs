using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.EstrategiasFila;

/// <summary>
/// Uma execução da estratégia: o que entrou (parâmetros com travas), o que o mundo era naquele
/// momento (cenário), o que a simulação projetou e — quando foi o agente — o que ele propôs.
///
/// <para><b>Append-only.</b> Rodada nunca é editada nem apagada: reabrir a estratégia e rodar de
/// novo cria a rodada seguinte. É isso que permite comparar "o que eu pedi na terça com o que o
/// agente sugeriu na quinta" e, depois de aplicada, medir a fila real contra a projeção.</para>
///
/// <para>Os campos <c>*Json</c> guardam os DTOs do Core serializados. São jsonb para consulta
/// ad hoc, não para índice — ninguém filtra por dentro deles.</para>
/// </summary>
public sealed class EstrategiaFilaRodada
{
    public Guid Id { get; set; }

    public Guid EstrategiaId { get; set; }
    public EstrategiaFila? Estrategia { get; set; }

    /// <summary>1, 2, 3… dentro da estratégia.</summary>
    public int Numero { get; set; }

    public ModoRodadaEstrategia Modo { get; set; }

    /// <summary>Parâmetros como chegaram (com as travas).</summary>
    public string ParametrosEntradaJson { get; set; } = "{}";

    /// <summary>Retrato do cenário atual no momento da rodada (fila, entrada, vazão, oferta).</summary>
    public string CenarioJson { get; set; } = "{}";

    /// <summary>Parâmetros ao fim da rodada — iguais aos de entrada no modo manual; os livres
    /// preenchidos pelo agente no modo agente.</summary>
    public string ParametrosResultadoJson { get; set; } = "{}";

    /// <summary>Série semanal + marcos (semana em que zera, equilíbrio, pico).</summary>
    public string ProjecaoJson { get; set; } = "{}";

    /// <summary>Resumo, ações, riscos e confiança do agente. Nulo no modo manual.</summary>
    public string? PropostaJson { get; set; }

    public string? Modelo { get; set; }
    public long TokensEntrada { get; set; }
    public long TokensSaida { get; set; }
    public decimal? CustoUsd { get; set; }
    public int DuracaoMs { get; set; }

    /// <summary>Preenchido quando a rodada não produziu proposta válida (agente sem ferramenta
    /// terminal, trava violada até o teto, API fora). A rodada fica registrada mesmo assim.</summary>
    public string? Falha { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
