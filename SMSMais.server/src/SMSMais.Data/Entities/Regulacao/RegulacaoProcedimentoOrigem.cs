using Pgvector;

using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Como um procedimento canônico aparece em UM sistema de regulação — o recurso do SER, o do
/// SERNIT, o procedimento do SISREG (ADR-0052).
///
/// <para><b>O embedding vive aqui, não no canônico.</b> Cada sistema escreve o mesmo assunto de
/// um jeito (<c>Ambulatório 1ª vez - Cardiologia</c> × <c>CONSULTA EM CARDIOLOGIA</c> ×
/// <c>Cardiologia</c>), e é justamente essa variedade de redação que faz a busca por texto livre
/// funcionar. Embedar só o nome canônico jogaria fora as outras duas formas de escrever.</para>
///
/// <para><b>Nunca é apagada.</b> Origem que sumiu do catálogo de sistema vira
/// <see cref="Ativo"/> = false: solicitação antiga continua apontando para o que foi escolhido
/// no dia, e o rótulo de então é o que a auditoria precisa ver.</para>
/// </summary>
public sealed class RegulacaoProcedimentoOrigem
{
    public Guid Id { get; set; }

    public Guid ProcedimentoId { get; set; }
    public RegulacaoProcedimento? Procedimento { get; set; }

    public SistemaRegulacao Sistema { get; set; }

    /// <summary>
    /// Identidade da origem dentro do seu sistema, e é ela que o upsert do sync usa.
    /// SISREG: o <c>pa</c> de 7 dígitos. SER: <c>{tipo}|{valor}|{AE|NAO_AE}</c> — o ramo entra
    /// porque o mesmo <c>valor</c> existe nos dois ramos com formulários diferentes.
    /// SERNIT: <c>{tipo}|{valor}</c>.
    /// </summary>
    public string ChaveExterna { get; set; } = string.Empty;

    /// <summary>Rótulo exatamente como o sistema de origem escreve.</summary>
    public string RotuloExterno { get; set; } = string.Empty;

    /// <summary>
    /// Ramo do SER: <c>"AE"</c> (ambulatório estadual, manual CRECE) ou <c>"NAO_AE"</c> (rede
    /// geral, manual REUNI). Nulo nos outros sistemas. Muda o formulário e as regras do mesmo
    /// recurso, por isso faz parte da identidade.
    /// </summary>
    public string? Ramo { get; set; }

    public Guid? SisregProcedimentoSigtapId { get; set; }
    public Guid? SerCatalogoRecursoId { get; set; }
    public Guid? SernitCatalogoRecursoId { get; set; }

    /// <summary>Embedding do rótulo (vector(1024), mesmo provedor do módulo de IA).</summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// SHA-256 de <c>modelo|texto</c>. É o que evita re-embedar 560 origens a cada sync: só
    /// reprocessa quem tem hash diferente. Trocar de modelo invalida tudo sozinho.
    /// </summary>
    public string? EmbeddingHash { get; set; }

    public DateTime? EmbeddingEm { get; set; }

    public VinculoOrigemRegulacao Vinculo { get; set; } = VinculoOrigemRegulacao.Automatico;

    /// <summary>Canônico que o pareamento sugere para esta origem. Só a curadoria confirma.</summary>
    public Guid? SugeridoProcedimentoId { get; set; }
    public double? SugeridoScore { get; set; }

    public DateTime? ConfirmadoEm { get; set; }
    public Guid? ConfirmadoPor { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
