namespace SMSMais.Data.Entities.KlinikosWeb;

/// <summary>Estado de um item na fila lenta do "deep" do conector web do Klinikos.</summary>
public enum KlinikosDeepEstado
{
    /// <summary>Esperando ser drenado (buscar narrativa/prescrição/vitais por tela).</summary>
    Enfileirado = 0,

    /// <summary>Em processamento por um drenador (evita dois pegarem o mesmo item).</summary>
    Processando = 1,

    /// <summary>Deep concluído — os recursos do boletim já convergiram no hub.</summary>
    Concluido = 2,

    /// <summary>Falhou depois das tentativas; fica para inspeção/re-enfileiramento manual.</summary>
    Falho = 3,
}

/// <summary>
/// Fila DURÁVEL do "deep" do conector web do Klinikos (ADITIVO — ver
/// <c>Integracoes/KlinikosWeb</c>). A espinha (Encounter/CID/desfecho/cadastro) entra em lote
/// pelos relatórios; a camada profunda (narrativa médica, prescrição, sinais vitais) só existe
/// por TELA, boletim a boletim — cara e frágil. Por isso ela não é puxada na hora: o boletim
/// fechado é ENFILEIRADO aqui e um drenador de fundo consome a taxa gentil, sem pressa, no vale
/// da madrugada (ver <c>Automais.klinikos/docs/sincronismo-cirurgico.md §3d</c>).
///
/// <para>A fila desacopla chegada de processamento: o sistema de origem nunca leva rajada. Ela
/// pode acumular sem dor — existe justamente para absorver o pico (~70 boletins/h no Conde) e
/// drenar devagar. Idempotente por (<see cref="Provedor"/>, <see cref="SpaCodigo"/>).</para>
///
/// <para><b>Nesta etapa só a fila e o enfileiramento existem</b> — o drenador ainda não. Nada
/// consome esta tabela automaticamente.</para>
/// </summary>
public class KlinikosDeepFila
{
    public Guid Id { get; set; }

    /// <summary>Provedor da instância (<c>klinikos_conde</c>/<c>klinikos_upa</c>/<c>klinikos_santarita</c>).</summary>
    public string Provedor { get; set; } = string.Empty;

    /// <summary>Nº do boletim (<c>spa_codigo</c>), já normalizado a 12 dígitos.</summary>
    public string SpaCodigo { get; set; } = string.Empty;

    /// <summary>Código da unidade na origem (0005 Conde, 0006 UPA, 0007 Santa Rita).</summary>
    public string UnidCodigo { get; set; } = string.Empty;

    /// <summary>Prioridade de drenagem — MENOR = mais urgente (internação/óbito/remoção antes de alta).</summary>
    public int Prioridade { get; set; }

    public KlinikosDeepEstado Estado { get; set; } = KlinikosDeepEstado.Enfileirado;

    /// <summary>Quantas vezes o drenador já tentou (para backoff/desistência).</summary>
    public int Tentativas { get; set; }

    /// <summary>Primeira linha do último erro, quando <see cref="Estado"/> = <c>Falho</c>.</summary>
    public string? UltimaMensagem { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
