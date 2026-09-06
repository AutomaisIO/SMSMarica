using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Uma exigência documental da solicitação — a "caixinha" onde entram os anexos daquele item.
///
/// <para>É uma caixinha por documento pedido, e não um monte único de anexos, porque o sistema
/// de regulação critica <b>um</b> documento específico ("laudo ilegível"): sem separar, ninguém
/// sabe qual arquivo trocar.</para>
///
/// <para><see cref="RegraId"/> nulo é a caixinha "Anexos gerais", que existe mesmo quando o
/// procedimento não tem regra documental cadastrada.</para>
/// </summary>
public sealed class RegulacaoSolicitacaoExigencia
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public RegulacaoSolicitacao? Solicitacao { get; set; }

    /// <summary>
    /// Regra de elegibilidade que originou a exigência (plano 03, incremento 4). Nulo = "Anexos
    /// gerais". Fica sem FK física por enquanto: `regulacao_regra` só nasce no incremento 4.
    /// </summary>
    public Guid? RegraId { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public bool Obrigatoria { get; set; }

    public SituacaoExigenciaRegulacao Situacao { get; set; } = SituacaoExigenciaRegulacao.Pendente;

    /// <summary>
    /// Exame que o próprio SMSMais já tinha e que o operador aceitou no lugar do anexo (R-09).
    /// Sem FK física: a origem pode ser imagem ou laudo, e nenhuma das duas é dona da exigência.
    /// </summary>
    public Guid? ExameInternoExameImagemId { get; set; }
    public Guid? ExameInternoLaudoId { get; set; }

    /// <summary>Quem validou que o exame interno serve. É decisão de gente, não do sistema.</summary>
    public Guid? ValidadoExamePor { get; set; }
    public DateTime? ValidadoExameEm { get; set; }

    /// <summary>Texto da crítica que veio do sistema de regulação.</summary>
    public string? CriticaTexto { get; set; }

    public int Ordem { get; set; }

    public ICollection<RegulacaoExigenciaArquivo> Arquivos { get; set; } = [];
}

/// <summary>
/// Um arquivo dentro de uma caixinha, com versão.
///
/// <para><b>Nada é apagado.</b> Documento criticado vira <see cref="SituacaoArquivoExigencia.Criticado"/>
/// e o novo entra como versão seguinte apontando para ele em <see cref="SubstituiArquivoId"/> —
/// é o histórico que explica, meses depois, por que a solicitação demorou.</para>
///
/// <para>O conteúdo mora no <b>Spaces</b>, não no banco (decisão do Bernardo em 05/09/2026): o
/// caminho real é foto de celular tirada no balcão, e não PDF pequeno. Aqui fica só a chave.</para>
/// </summary>
public sealed class RegulacaoExigenciaArquivo
{
    public Guid Id { get; set; }

    public Guid ExigenciaId { get; set; }
    public RegulacaoSolicitacaoExigencia? Exigencia { get; set; }

    /// <summary>Chave no Spaces: <c>Regulacao/{pacienteId}/{id}.{ext}</c>.</summary>
    public string ChaveArmazenamento { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanho { get; set; }

    /// <summary>Hash do conteúdo — detecta reenvio do mesmo arquivo e confere integridade.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>1..n dentro da caixinha.</summary>
    public int Versao { get; set; }

    public Guid? SubstituiArquivoId { get; set; }

    public SituacaoArquivoExigencia Situacao { get; set; } = SituacaoArquivoExigencia.Atual;
    public OrigemArquivoExigencia Origem { get; set; } = OrigemArquivoExigencia.Upload;

    /// <summary>Quando o arquivo chegou ao sistema de regulação. Nulo = ainda não subiu.</summary>
    public DateTime? EnviadoAoSistemaEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
