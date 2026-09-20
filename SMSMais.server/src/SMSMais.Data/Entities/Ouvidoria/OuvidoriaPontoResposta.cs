using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Quem responde pela manifestação depois do encaminhamento: uma unidade de saúde, uma área
/// central (Regulação, Farmácia…) ou uma unidade apuratória (D-6). Cadastrado pelo ouvidor;
/// os <see cref="Membros"/> são os usuários que veem e respondem pelo ponto — sem nunca ver o
/// manifestante.
/// </summary>
public sealed class OuvidoriaPontoResposta
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public OuvidoriaTipoPontoResposta Tipo { get; set; }

    /// <summary>Unidade de saúde correspondente. Obrigatória quando <c>Tipo = Unidade</c>; uma unidade tem no máximo um ponto.</summary>
    public Guid? UnidadeId { get; set; }

    /// <summary>Prazo próprio em dias para responder à ouvidoria; nulo = usa o da configuração por prioridade.</summary>
    public int? PrazoDias { get; set; }

    public bool Ativo { get; set; } = true;

    // ---- Auditoria ADR-0006 ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }

    public Unidade? Unidade { get; set; }
    public ICollection<OuvidoriaPontoRespostaMembro> Membros { get; set; } = [];
}
