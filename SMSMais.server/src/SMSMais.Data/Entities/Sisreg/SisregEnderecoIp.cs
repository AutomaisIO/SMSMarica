namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Um período em que o SISREG (<c>sisregiii.saude.gov.br</c>) respondeu num IP. O SISREG troca de IP
/// sem aviso — em 09/10/2026 foi para trás do F5 (189.28.130.13 → 159.60.146.75) — e a produção só o
/// alcança pelo túnel <c>wg-eveo</c>, que só aceita IP fixo (docs/sisreg-egress.md). Quem põe o IP
/// novo no túnel é o timer <c>sisreg-egress-verificar</c> do servidor; esta tabela é o que a
/// plataforma VÊ: o IP atual, desde quando e por onde a rota sai.
///
/// <para>O período abre quando o DNS passa a devolver o IP e fecha (<see cref="AteEm"/>) quando ele
/// some do DNS por um tempo. Abertos = IP(s) atual(is); a lista inteira é o histórico das trocas.</para>
/// </summary>
public class SisregEnderecoIp
{
    public Guid Id { get; set; }

    public string Ip { get; set; } = string.Empty;

    /// <summary>Primeira verificação em que o DNS devolveu este IP (UTC). O "desde" da tela.</summary>
    public DateTime DesdeEm { get; set; }

    /// <summary>Última verificação em que o DNS ainda devolvia este IP (UTC).</summary>
    public DateTime UltimaVezVistoEm { get; set; }

    /// <summary>Quando o período foi encerrado por o IP ter saído do DNS (UTC). Nulo = IP atual.</summary>
    public DateTime? AteEm { get; set; }

    /// <summary>
    /// Interface por onde a rota do servidor até este IP saía na última verificação (ex.: <c>wg-eveo</c>,
    /// <c>eth0</c>). Nulo quando não deu para ler (fora do Linux, comando indisponível).
    /// </summary>
    public string? InterfaceRota { get; set; }
}
