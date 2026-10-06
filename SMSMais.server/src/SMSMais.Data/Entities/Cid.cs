namespace SMSMais.Data.Entities;

/// <summary>
/// Catálogo CID-10 CANÔNICO da instância — a nossa tabela. Consolidado a partir dos espelhos
/// raspados do SER (<c>ser_catalogo_cid</c>) e do SERNIT (<c>sernit_catalogo_cid</c>), que sozinhos
/// têm buracos (o SER não traz o capítulo Z — Z00, Z01x… — justamente os CID de rastreamento que
/// mais aparecem em exame de imagem). Assim a descrição do "diagnóstico inicial" do pedido
/// (ticket #155) sai da NOSSA base e não depende de qual sistema de regulação respondeu.
///
/// <para>A tabela é preenchida por <c>ICidCatalogoSyncService</c> (sob demanda e automaticamente,
/// ao fim da importação dos catálogos SER/SERNIT). Nunca por migration — os espelhos são dados
/// específicos da instância.</para>
/// </summary>
public class Cid
{
    /// <summary>Código CID-10, chave natural. Normalizado como as origens guardam: sem ponto,
    /// caixa alta (ex.: <c>Z014</c>, <c>M545</c>).</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Descrição por extenso.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>De qual espelho veio a descrição (<c>SER</c> ou <c>SERNIT</c>) — proveniência.</summary>
    public string? Fonte { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
