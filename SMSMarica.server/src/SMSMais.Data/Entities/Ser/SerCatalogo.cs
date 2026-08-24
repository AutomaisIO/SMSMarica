namespace SMSMais.Data.Entities.Ser;

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

    /// <summary>
    /// O ramo do combo "É AMBULATÓRIO ESTADUAL?" (<c>form0:comboSisReg</c>) em que este recurso
    /// aparece. <b>Faz parte da identidade</b>, não é um atributo.
    ///
    /// <para>Medido em 10/08/2026: o ramo muda a lista E o formulário. CONSULTA lista 120 recursos
    /// no "Não" e 151 no "Sim" — 31 consultas (urologia, pneumologia, reumatologia, fonoaudiologia…)
    /// só existem no "Sim" e ficaram fora da nossa primeira cópia. EXAME lista 83 e 64. E o mesmo
    /// recurso pede formulários diferentes: o 1000 pede 9 campos no "Não" e 3 no "Sim".</para>
    ///
    /// <para>Por isso o par (tipo, valor) NÃO identifica um recurso — só (tipo, valor, ramo).</para>
    /// </summary>
    public bool AmbulatorioEstadual { get; set; }

    /// <summary>O <c>value</c> do combo no SER — é ele que viaja no envio.</summary>
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Quando a sincronização confirmou este recurso pela última vez. Recurso que para
    /// de aparecer no SER fica com a data velha — é assim que se enxerga o que saiu do ar.</summary>
    public DateTime SincronizadoEm { get; set; }

    /// <summary>
    /// Qual lista de CID este recurso aceita na Hipótese. Nulo = ainda não medido, e a tela cai
    /// no autocomplete ao vivo do SER.
    ///
    /// <para>É FK e não um enum porque o agrupamento é medido, não decretado: em 20/08/2026 os
    /// 422 recursos dos dois ramos produziram <b>duas</b> listas, mas isso é um fato do SER de
    /// hoje. Se a SES-RJ criar uma terceira, a cópia a descobre sozinha.</para>
    /// </summary>
    public Guid? CidListaId { get; set; }
    public SerCatalogoCidLista? CidLista { get; set; }

    /// <summary>
    /// A assinatura medida para este recurso (<c>78|395|90</c>). Fica gravada junto porque a
    /// medição e a cópia são passadas SEPARADAS: medir percorre os 422 recursos numa conversa
    /// Seam só, e copiar uma lista reabre a aba — fazer as duas coisas intercaladas destruiria o
    /// estado da conversa e as assinaturas seguintes sairiam do recurso errado.
    /// </summary>
    public string? CidAssinatura { get; set; }

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

/// <summary>
/// Uma relação de CID que o SER aceita na Hipótese — <b>a lista, não o recurso</b>.
///
/// <para><b>Por que agrupar:</b> medido em 20/08/2026 (<c>Automais.SER/probe_cid_grupos.py</c>),
/// os 422 recursos dos dois ramos produzem apenas <b>duas</b> listas: a ampla, com 14.226 CID —
/// o CID-10 inteiro —, usada por 390 recursos, e uma de 136 códigos, usada pelos 32 recursos
/// oncológicos (Hematologia, Mastologia, Urologia, Coloproctologia… todos com "(Oncologia)" no
/// nome). A restrita é subconjunto perfeito da ampla: nenhum código fora dela.</para>
///
/// <para>Guardar por LISTA, e não por recurso, é o que torna a cópia viável: varre-se cada lista
/// uma vez (260 buscas por prefixo, ~25s) em vez de uma vez por recurso.</para>
/// </summary>
public class SerCatalogoCidLista
{
    public Guid Id { get; set; }

    /// <summary>
    /// Como os recursos são reconhecidos como sendo da mesma lista: as contagens de três buscas
    /// de sondagem, na forma <c>78|395|90</c>. Duas listas iguais dão a mesma assinatura, e é por
    /// ela que um recurso novo é ligado a uma lista já copiada sem varrer nada de novo.
    /// </summary>
    public string Assinatura { get; set; } = string.Empty;

    /// <summary>Quantos CID esta lista tem — para a tela dizer o tamanho sem contar linha.</summary>
    public int Quantidade { get; set; }

    public DateTime SincronizadoEm { get; set; }

    public ICollection<SerCatalogoCid> Cids { get; set; } = [];
}

/// <summary>Um CID de uma lista, do jeito que o SER o devolve.</summary>
public class SerCatalogoCid
{
    public Guid Id { get; set; }

    public Guid ListaId { get; set; }
    public SerCatalogoCidLista? Lista { get; set; }

    /// <summary>Código sem ponto, como o SER usa: <c>A09</c>, <c>E119</c>.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// O texto que o SER escreve no campo ao clicar na sugestão — <c>(A09 ) Diarréia e
    /// gastroenterite…</c>. É ele que o pedido leva de volta em <c>form0:procedimento</c>, e por
    /// isso é copiado como veio, sem normalizar acento nem espaço.
    /// </summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>
    /// Código e descrição sem acento e em minúsculas, para a busca da tela.
    ///
    /// <para>O SER casa "contém" e o dado dele é irregular ("Hipertensao" sem til, "Diarréia"
    /// com acento). Comparar pelo texto cru faria "hipertensão" não achar nada. Isto não amplia
    /// o que é oferecido — o conjunto continua sendo o do SER —, só torna encontrável o que já
    /// está lá.</para>
    /// </summary>
    public string Busca { get; set; } = string.Empty;
}
