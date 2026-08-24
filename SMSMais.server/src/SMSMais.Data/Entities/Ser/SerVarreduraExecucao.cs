using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ser;

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

    // ---- Ponteiro de retomada ----
    // A rodada é longa (grade + histórico de milhares). Deploy, restart ou queda no meio NÃO
    // podem custar a rodada inteira: o ponteiro guarda onde parou e a execução continua
    // PENDENTE até terminar de verdade.

    /// <summary>Fase em que a execução está — define o que retomar.</summary>
    public FaseVarreduraSer Fase { get; set; } = FaseVarreduraSer.Grade;

    /// <summary>Situação que estava sendo varrida quando parou (fase de grade).</summary>
    public SituacaoSer? CursorSituacao { get; set; }

    /// <summary>
    /// Data da Solicitação a partir da qual continuar. É o cursor do varredor por export: cada
    /// lote devolve até 500 e o ponteiro avança para a última data lida.
    /// </summary>
    public DateOnly? CursorData { get; set; }

    /// <summary>Último <c>IdSer</c> cujo histórico foi lido com sucesso (fase de histórico).</summary>
    public string? CursorIdSer { get; set; }

    /// <summary>Quantas solicitações a fase de histórico ainda tem para ler. Alimenta a barra de
    /// progresso da tela sem precisar recontar a fila toda a cada refresh.</summary>
    public int HistoricosPendentes { get; set; }

    /// <summary>Quantas vezes esta execução foi retomada após parada. Mais de duas ou três
    /// seguidas é sinal de que o serviço está reiniciando sozinho.</summary>
    public int Retomadas { get; set; }

    /// <summary>Quando foi retomada pela última vez.</summary>
    public DateTime? RetomadaEm { get; set; }

    /// <summary>
    /// <b>Sinal de vida:</b> quando o motor gravou progresso pela última vez.
    ///
    /// <para>Existe porque nenhum outro campo responde a única pergunta que importa quando uma
    /// rodada demora: <i>está trabalhando ou pendurada?</i> Contador parado é ambíguo — pode ser
    /// uma fatia grande em andamento ou o processo travado. Em 08/08/2026 essa dúvida só foi
    /// resolvida indo ao <c>journalctl</c> do servidor, o que ninguém que opera a tela vai
    /// fazer.</para>
    ///
    /// <para>É atualizado a cada gravação de progresso — por lote na fase de grade, por
    /// solicitação na de histórico. A tela mostra "há Xs"; envelheceu, é porque parou.</para>
    /// </summary>
    public DateTime? UltimoSinalEm { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    /// <summary>Quem disparou. NULL no disparo agendado — a autoria é o <see cref="Disparo"/>.</summary>
    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
}

/// <summary>Fase da varredura — o que retomar quando a execução volta.</summary>
public enum FaseVarreduraSer
{
    /// <summary>Lendo a grade (espelho das solicitações).</summary>
    Grade = 1,

    /// <summary>Lendo o histórico das solicitações que precisam.</summary>
    Historico = 2,

    /// <summary>Nada mais a fazer.</summary>
    Finalizada = 3,
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

    /// <summary>
    /// <b>Parada por queda do serviço, e RETOMÁVEL.</b> Deploy/restart/crash no meio da rodada
    /// não podem custar horas de trabalho nem fingir que terminou: a execução permanece
    /// pendente, o ponteiro guarda onde parou, e o runner retoma quando o serviço sobe.
    ///
    /// <para>Status próprio, e não <see cref="Erro"/>, porque erro é algo que exige alguém olhar
    /// — e isto aqui se resolve sozinho.</para>
    /// </summary>
    Interrompida = 7,
}
