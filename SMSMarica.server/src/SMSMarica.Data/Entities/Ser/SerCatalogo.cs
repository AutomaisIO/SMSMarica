namespace SMSMarica.Data.Entities.Ser;

/// <summary>
/// Um recurso do catálogo do SER (o que se pode pedir), espelhado na nossa base.
///
/// <para><b>Por que espelhar:</b> a tela de nova solicitação lia o catálogo AO VIVO — três idas
/// ao SER só para montar o formulário, e nada funcionava se o SER estivesse fora. Com o espelho,
/// a tela é instantânea e <b>offline</b>; o SER só é procurado quando o pedido for de fato
/// enviado, que é quando ele precisa mesmo estar de pé.</para>
///
/// <para>Medido em 08/08/2026: 120 recursos de CONSULTA e 83 de EXAME.</para>
/// </summary>
public class SerCatalogoRecurso
{
    public Guid Id { get; set; }

    public TipoRecursoSer Tipo { get; set; }

    /// <summary>O <c>value</c> do combo no SER — é ele que viaja no envio.</summary>
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Quando a sincronização confirmou este recurso pela última vez. Recurso que para
    /// de aparecer no SER fica com a data velha — é assim que se enxerga o que saiu do ar.</summary>
    public DateTime SincronizadoEm { get; set; }

    /// <summary><c>true</c> quando os campos dinâmicos deste recurso já foram lidos. A varredura
    /// do catálogo é longa (uma ida ao SER por recurso) e pode ser retomada.</summary>
    public bool CamposLidos { get; set; }

    public ICollection<SerCatalogoCampo> Campos { get; set; } = [];
}

/// <summary>
/// Um campo do bloco DINÂMICO — o que o SER acrescenta conforme o recurso.
///
/// <para>É o que faz oncologia pedir peso, altura, IMC e datas de biópsia, PET-CT pedir grau
/// histopatológico, e cardiologia pedir NYHA e grupo sanguíneo. São 163 campos únicos no
/// catálogo, distribuídos em 21 formulários distintos.</para>
/// </summary>
public class SerCatalogoCampo
{
    public Guid Id { get; set; }

    public Guid RecursoId { get; set; }
    public SerCatalogoRecurso? Recurso { get; set; }

    /// <summary>O <c>N</c> de <c>form0:dinamico_id_N</c> — identidade do campo no SER.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Nome JSF completo do campo, como viaja no envio.</summary>
    public string Campo { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary><c>text</c>, <c>textarea</c>, <c>select</c>, <c>radio</c> ou <c>checkbox</c>.</summary>
    public string Tipo { get; set; } = string.Empty;

    public bool Obrigatorio { get; set; }

    /// <summary>Opções de combo/rádio em JSON. Fica como JSON porque a forma varia por campo e
    /// nada no nosso lado consulta opção isoladamente — só renderiza.</summary>
    public string? OpcoesJson { get; set; }

    /// <summary>Ordem em que o SER apresenta. Reordenar confundiria quem preenche olhando a
    /// tela do SER ao lado.</summary>
    public int Ordem { get; set; }
}

/// <summary>
/// Listas do bloco FIXO do formulário (classificação de risco, médicos, "é ambulatório
/// estadual?"). Uma tabela genérica em vez de uma por lista: são catálogos pequenos, de forma
/// idêntica, e nenhum tem regra própria.
/// </summary>
public class SerCatalogoLista
{
    public Guid Id { get; set; }

    /// <summary>Qual lista: <c>medico</c>, <c>classificacao_risco</c>, <c>ambulatorio_estadual</c>.</summary>
    public string Lista { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public DateTime SincronizadoEm { get; set; }
}
