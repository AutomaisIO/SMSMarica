using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Integracoes.SernitWeb.Varredura;

/// <summary>
/// Um recorte que estourou o teto de 100 da tela do SERNIT mesmo depois de reduzido a
/// <b>um dia + um tipo</b> — significa <b>registros não lidos</b>. Declarado de propósito: a rodada
/// vira <c>Parcial</c> em vez de <c>Concluída</c>.
/// </summary>
public sealed record FatiaTruncadaSernit(SituacaoSernit Situacao, DateOnly Dia, TipoRecursoSernit? Tipo);

/// <summary>Contadores de uma varredura por situação (por paginação).</summary>
public sealed class ResultadoVarreduraSernit
{
    /// <summary>Buscas (POST de pesquisa) gastas — cresce com o nº de fatias da bisecção.</summary>
    public int Buscas { get; set; }

    /// <summary>Páginas do datascroller lidas.</summary>
    public int Paginas { get; set; }

    /// <summary>Fatias que estouraram o teto mesmo em 1 dia + 1 tipo. Nunca truncamos em silêncio.</summary>
    public List<FatiaTruncadaSernit> Truncadas { get; } = [];
}
