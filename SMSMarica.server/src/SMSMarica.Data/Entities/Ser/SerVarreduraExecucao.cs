using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Ser;

/// <summary>Modo da rodada — muda o custo e o que é lido.</summary>
public enum ModoVarreduraSer
{
    /// <summary><b>Carga inicial:</b> varre todas as situações desde o início e lê o histórico de
    /// TUDO. Roda uma vez (ou sob demanda para refazer a base).</summary>
    CargaInicial = 1,

    /// <summary><b>Diária:</b> varre todas as situações, faz diff de situação, e relê o histórico
    /// só de quem mudou de situação + de todos em <c>EmFila</c> (por causa do FollowUP).</summary>
    Diaria = 2,

    /// <summary><b>Só a grade:</b> atualiza situações sem tocar em histórico. Rodada barata para
    /// diagnóstico ou para recuperar de uma parada no meio.</summary>
    SomenteGrade = 3,
}

/// <summary>
/// Uma execução do motor de varredura do SER — ADR-0042.
///
/// <para>Rastreio próprio, no padrão de <c>SisregVarreduraExecucao</c>. O contador que importa
/// aqui é <see cref="FatiasTruncadas"/>: como a tela do SER corta em 100 registros, uma fatia que
/// não coube significa <b>cobertura incompleta</b>, e marcar a rodada como concluída nesse caso
/// faria o operador acreditar que a base está completa quando não está.</para>
/// </summary>
public class SerVarreduraExecucao
{
    public Guid Id { get; set; }

    public ModoVarreduraSer Modo { get; set; } = ModoVarreduraSer.Diaria;

    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;

    public StatusVarreduraSer Status { get; set; } = StatusVarreduraSer.Pendente;

    /// <summary>Janela de <c>Data da Solicitação</c> varrida (eixo imutável do fatiamento).</summary>
    public DateOnly JanelaInicio { get; set; }
    public DateOnly JanelaFim { get; set; }

    /// <summary>Situações incluídas nesta rodada, separadas por vírgula. Guardado porque uma
    /// rodada pode ser parcial por escopo (só EM_FILA) e não por falha.</summary>
    public string SituacoesVarridas { get; set; } = string.Empty;

    // ---- Contadores da grade ----

    /// <summary>Buscas (POST de pesquisa) gastas. Com a bisecção, cresce com o nº de fatias.</summary>
    public int Buscas { get; set; }

    /// <summary>Páginas do datascroller lidas.</summary>
    public int Paginas { get; set; }

    /// <summary>Solicitações únicas vistas (deduplicadas por <c>IdSer</c>).</summary>
    public int SolicitacoesEncontradas { get; set; }

    public int SolicitacoesNovas { get; set; }
    public int SolicitacoesAtualizadas { get; set; }

    /// <summary>Quantas mudaram de situação — o número que o operador quer ver.</summary>
    public int MudancasSituacao { get; set; }

    // ---- Contadores do histórico ----

    public int HistoricosLidos { get; set; }
    public int EventosNovos { get; set; }
    public int FollowUpsNovos { get; set; }

    /// <summary>Solicitações cujo menu não oferece histórico (situação Alta).</summary>
    public int HistoricosIndisponiveis { get; set; }

    public int GatilhosGerados { get; set; }

    /// <summary>
    /// <b>Fatias que estouraram o teto de 100 mesmo em 1 dia + 1 tipo.</b> Maior que zero
    /// significa que existem registros que NÃO foram lidos. O detalhe de cada fatia vai para
    /// <see cref="SerVarreduraFalha"/>.
    /// </summary>
    public int FatiasTruncadas { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    /// <summary>Quem disparou. NULL no disparo agendado — a autoria é o <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
}

/// <summary>Estado de uma varredura do SER.</summary>
public enum StatusVarreduraSer
{
    Pendente = 1,
    EmExecucao = 2,

    /// <summary>Varreu tudo que se propôs, sem fatia truncada.</summary>
    Concluida = 3,

    /// <summary>
    /// <b>Cobertura incompleta, declarada.</b> Terminou, mas alguma fatia estourou o teto de 100
    /// do SER ou o histórico de alguém não pôde ser lido. Status próprio porque marcar
    /// "Concluída" aqui faria o operador concluir que a base está completa.
    /// </summary>
    Parcial = 4,

    Erro = 5,
    Cancelada = 6,
}
