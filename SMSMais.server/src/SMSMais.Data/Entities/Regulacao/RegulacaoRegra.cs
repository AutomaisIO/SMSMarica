using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Um critério de elegibilidade de um procedimento, como o manual da regulação escreve
/// (ADR-0052, plano 03).
///
/// <para><b>A fonte é o manual, e o texto literal fica guardado.</b> `Descricao` carrega o que
/// está escrito no CRECE/REUNI, sem reescrita: quando a regulação recusar um pedido citando o
/// manual, a pessoa da unidade precisa achar a mesma frase aqui — parafrasear cria duas versões
/// da regra e a discussão vira sobre qual das duas vale.</para>
///
/// <para><b>Versionada, nunca editada em silêncio.</b> Corrigir uma regra cria a versão seguinte
/// e desativa a anterior; a resposta que o solicitante deu fica presa à versão que ele viu. Sem
/// isso, mudar o manual reescreveria retroativamente por que um pedido foi barrado.</para>
/// </summary>
public sealed class RegulacaoRegra
{
    public Guid Id { get; set; }

    public Guid ProcedimentoId { get; set; }
    public RegulacaoProcedimento? Procedimento { get; set; }

    /// <summary>
    /// Regra de um recurso específico, e não do canônico inteiro. O catálogo é plano (D-10): o
    /// balde e o específico são entradas distintas, e o manual às vezes fala só de uma delas.
    /// </summary>
    public Guid? ProcedimentoOrigemId { get; set; }
    public RegulacaoProcedimentoOrigem? ProcedimentoOrigem { get; set; }

    /// <summary><c>null</c> = vale para todos os sistemas de destino.</summary>
    public SistemaRegulacao? Sistema { get; set; }

    public TipoRegraRegulacao Tipo { get; set; }
    public SeveridadeRegraRegulacao Severidade { get; set; }

    /// <summary>O texto do manual, literal.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>De onde saiu — manual, volume, página. É o que sustenta a regra numa discussão.</summary>
    public string? Fonte { get; set; }

    // ---- dedutível: o sistema decide sozinho, pelo cadastro ----

    public int? IdadeMinAnos { get; set; }
    public int? IdadeMaxAnos { get; set; }

    /// <summary><c>"M"</c>, <c>"F"</c> ou nulo.</summary>
    public string? Sexo { get; set; }

    public bool ExigeCpf { get; set; }

    /// <summary>Prefixos de CID-10 (<c>["I10", "C50"]</c>) — o casamento é por início do código.</summary>
    public string? CidsPermitidosJson { get; set; }
    public string? CidsExcluidosJson { get; set; }

    public string? MunicipiosIbgeJson { get; set; }

    /// <summary>Reservado para critérios compostos. Sempre nulo nesta fase.</summary>
    public string? ExpressaoJson { get; set; }

    // ---- não dedutível: vira pergunta ----

    public string? Pergunta { get; set; }

    /// <summary>
    /// Qual resposta barra. Vem do manual e <b>não se adivinha</b>: no spike e, deduzir isso do
    /// texto invertia 295 das 1.169 regras (a seção do manual é que decide).
    /// </summary>
    public RespostaRegraRegulacao? RespostaBloqueia { get; set; }

    /// <summary>O que fazer com "não sei". Nulo = o padrão da configuração do módulo.</summary>
    public NaoSeiViraRegulacao? NaoSeiVira { get; set; }

    // ---- documental: vira caixinha de anexo ----

    public string? DocumentoRotulo { get; set; }

    /// <summary>Quando o documento é um exame nosso, isto liga a caixinha ao acervo interno.</summary>
    public Guid? TipoExameId { get; set; }

    /// <summary>Por quantos dias o exame vale. Nulo = não expira.</summary>
    public int? ValidadeDias { get; set; }

    public bool Obrigatorio { get; set; } = true;

    public int Ordem { get; set; }

    public int Versao { get; set; } = 1;

    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}

/// <summary>
/// O que a solicitação respondeu a cada regra — inclusive o que o sistema deduziu sozinho.
///
/// <para><b>Guarda a versão da regra respondida.</b> É isso que permite reler, meses depois, por
/// que aquele pedido passou ou parou: com o manual atualizado desde então, a regra de hoje já não
/// é a que foi aplicada.</para>
/// </summary>
public sealed class RegulacaoSolicitacaoRespostaRegra
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public RegulacaoSolicitacao? Solicitacao { get; set; }

    public Guid RegraId { get; set; }
    public RegulacaoRegra? Regra { get; set; }

    public int RegraVersao { get; set; }

    public RespostaRegraRegulacao Resposta { get; set; }

    /// <summary>O dado que sustentou a dedução (<c>"idade=31"</c>) — a conta que o sistema fez.</summary>
    public string? ValorDeduzido { get; set; }

    public ResultadoRegraRegulacao Resultado { get; set; }

    public Guid? RespondidoPor { get; set; }
    public DateTime RespondidoEm { get; set; }
}
