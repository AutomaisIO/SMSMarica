namespace SMSMais.Data.Entities.Sernit;

/// <summary>
/// Um recurso do catálogo do SERNIT (o que se pode pedir), espelhado na nossa base. Espelho do
/// <c>SerCatalogoRecurso</c> do SER-RJ, mas <b>sem o ramo "ambulatório estadual"</b> — o SERNIT
/// não tem esse gate, então a chave natural é só (Tipo, Valor). Medido no lab: CONSULTA 43
/// recursos em 3 formulários, EXAME 35 em 5.
/// </summary>
public class SernitCatalogoRecurso
{
    public Guid Id { get; set; }

    public TipoRecursoSernit Tipo { get; set; }

    /// <summary>O <c>value</c> do combo `form0:comboRecurso` no SERNIT — é ele que viaja no envio.</summary>
    public string Valor { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    public DateTime SincronizadoEm { get; set; }

    /// <summary>Qual lista de CID este recurso aceita na Hipótese. Nulo = não medido; a tela cai no
    /// autocomplete ao vivo.</summary>
    public Guid? CidListaId { get; set; }
    public SernitCatalogoCidLista? CidLista { get; set; }

    /// <summary>Assinatura medida (contagens de buscas de sondagem) — liga recursos à mesma lista.</summary>
    public string? CidAssinatura { get; set; }

    /// <summary><c>true</c> quando os campos dinâmicos deste recurso já foram lidos (varredura
    /// retomável).</summary>
    public bool CamposLidos { get; set; }

    public ICollection<SernitCatalogoCampo> Campos { get; set; } = [];
}

/// <summary>Um campo do bloco DINÂMICO (`form0:campoDinamicoBox`), o que o SERNIT acrescenta
/// conforme o recurso.</summary>
public class SernitCatalogoCampo
{
    public Guid Id { get; set; }

    public Guid RecursoId { get; set; }
    public SernitCatalogoRecurso? Recurso { get; set; }

    /// <summary>Identidade do campo no SERNIT (o segmento numérico do name JSF).</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Nome JSF completo do campo, como viaja no envio.</summary>
    public string Campo { get; set; } = string.Empty;

    public string Rotulo { get; set; } = string.Empty;

    /// <summary><c>text</c>, <c>textarea</c>, <c>select</c>, <c>radio</c> ou <c>checkbox</c>.</summary>
    public string Tipo { get; set; } = string.Empty;

    public bool Obrigatorio { get; set; }

    /// <summary>Opções de combo/rádio em JSON (a forma varia por campo; nada consulta opção isolada).</summary>
    public string? OpcoesJson { get; set; }

    public int Ordem { get; set; }
}

/// <summary>Listas do bloco FIXO (classificação de risco, médicos). Tabela genérica.</summary>
public class SernitCatalogoLista
{
    public Guid Id { get; set; }

    /// <summary>Qual lista: <c>medico</c>, <c>classificacao_risco</c>.</summary>
    public string Lista { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;
    public string Rotulo { get; set; } = string.Empty;
    public int Ordem { get; set; }
    public DateTime SincronizadoEm { get; set; }
}

/// <summary>Uma relação de CID que o SERNIT aceita na Hipótese — a lista, não o recurso (recursos
/// com a mesma assinatura compartilham a lista).</summary>
public class SernitCatalogoCidLista
{
    public Guid Id { get; set; }

    /// <summary>Contagens de buscas de sondagem (<c>78|395|90</c>) — identidade da lista.</summary>
    public string Assinatura { get; set; } = string.Empty;

    public int Quantidade { get; set; }
    public DateTime SincronizadoEm { get; set; }

    public ICollection<SernitCatalogoCid> Cids { get; set; } = [];
}

/// <summary>Um CID de uma lista, do jeito que o SERNIT o devolve.</summary>
public class SernitCatalogoCid
{
    public Guid Id { get; set; }

    public Guid ListaId { get; set; }
    public SernitCatalogoCidLista? Lista { get; set; }

    /// <summary>Código sem ponto, como o SERNIT usa (<c>A09</c>, <c>E119</c>).</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    /// <summary>O texto que o SERNIT escreve no campo ao clicar na sugestão — <c>(A09 ) Diarréia…</c>.
    /// É ele que o pedido leva de volta em `form0:procedimento`; copiado como veio.</summary>
    public string Texto { get; set; } = string.Empty;

    /// <summary>Código+descrição sem acento e em minúsculas, para a busca da tela.</summary>
    public string Busca { get; set; } = string.Empty;
}
