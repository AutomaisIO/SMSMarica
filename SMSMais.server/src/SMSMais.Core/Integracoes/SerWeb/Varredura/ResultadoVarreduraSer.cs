using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Integracoes.SerWeb.Varredura;

/// <summary>
/// Um recorte que estourou o teto da tela mesmo depois de reduzido a <b>um dia + um tipo</b>.
///
/// <para>Significa <b>registros que não foram lidos</b>. É declarado de propósito: a rodada vira
/// <c>Parcial</c> em vez de <c>Concluída</c>, porque marcar "concluída" aqui faria quem opera
/// concluir que a base está completa quando não está.</para>
/// </summary>
public sealed record FatiaTruncada(SituacaoSer Situacao, DateOnly Dia, TipoRecursoSer? Tipo);

/// <summary>Contadores de uma varredura por situação.</summary>
public sealed class ResultadoVarreduraSer
{
    /// <summary>Quantos lotes foram pedidos ao SER (cada um custa uma busca + um download).</summary>
    public int Buscas { get; set; }

    /// <summary>Fatias que estouraram o teto mesmo em 1 dia + 1 tipo. Cada uma significa
    /// <b>registros não lidos</b> — nunca truncamos em silêncio.</summary>
    public List<FatiaTruncada> Truncadas { get; } = [];
}
