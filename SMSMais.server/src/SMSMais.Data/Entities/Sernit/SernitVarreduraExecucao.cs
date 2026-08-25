using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sernit;

/// <summary>Modo da rodada — muda o custo e o que é lido.</summary>
public enum ModoVarreduraSernit
{
    /// <summary><b>Carga inicial:</b> varre todas as situações desde o início e lê o histórico de tudo.</summary>
    CargaInicial = 1,

    /// <summary><b>Diária:</b> varre todas as situações, faz diff de situação, e relê o histórico só
    /// de quem mudou de situação + de todos em <c>EmFila</c> (por causa do FollowUP).</summary>
    Diaria = 2,

    /// <summary><b>Só a grade:</b> atualiza situações sem tocar em histórico.</summary>
    SomenteGrade = 3,
}

/// <summary>Fase da varredura — o que retomar quando a execução volta.</summary>
public enum FaseVarreduraSernit
{
    Grade = 1,
    Historico = 2,
    Finalizada = 3,
}

/// <summary>Estado de uma varredura do SERNIT.</summary>
public enum StatusVarreduraSernit
{
    Pendente = 1,
    EmExecucao = 2,

    /// <summary>Varreu tudo que se propôs, sem fatia truncada.</summary>
    Concluida = 3,

    /// <summary><b>Cobertura incompleta, declarada.</b> Alguma fatia estourou o teto de 100 do
    /// SERNIT ou um histórico não pôde ser lido. Status próprio para não parecer "Concluída".</summary>
    Parcial = 4,

    Erro = 5,
    Cancelada = 6,

    /// <summary><b>Parada por queda do serviço, e RETOMÁVEL.</b> Deploy/restart/crash no meio: a
    /// execução permanece pendente, o ponteiro guarda onde parou, e o runner retoma na subida.</summary>
    Interrompida = 7,
}

/// <summary>
/// Uma execução do motor de varredura do SERNIT — subsistema irmão do SER-RJ (ADR-0042).
///
/// <para>O contador que importa é <see cref="FatiasTruncadas"/>: como a grade do SERNIT corta em
/// 100 registros (mas informa o total real em <c>Total de resultados encontrados</c>), uma fatia
/// que não coube significa <b>cobertura incompleta</b> — marcar como concluída enganaria o operador.</para>
/// </summary>
public class SernitVarreduraExecucao
{
    public Guid Id { get; set; }

    public ModoVarreduraSernit Modo { get; set; } = ModoVarreduraSernit.Diaria;
    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;
    public StatusVarreduraSernit Status { get; set; } = StatusVarreduraSernit.Pendente;

    /// <summary>Janela de <c>Data da Solicitação</c> varrida (eixo imutável do fatiamento).</summary>
    public DateOnly JanelaInicio { get; set; }
    public DateOnly JanelaFim { get; set; }

    public string SituacoesVarridas { get; set; } = string.Empty;

    // ---- Contadores da grade ----
    public int Buscas { get; set; }
    public int Paginas { get; set; }
    public int SolicitacoesEncontradas { get; set; }
    public int SolicitacoesNovas { get; set; }
    public int SolicitacoesAtualizadas { get; set; }
    public int MudancasSituacao { get; set; }

    // ---- Contadores do histórico ----
    public int HistoricosLidos { get; set; }
    public int EventosNovos { get; set; }
    public int FollowUpsNovos { get; set; }
    public int HistoricosIndisponiveis { get; set; }
    public int GatilhosGerados { get; set; }

    /// <summary><b>Fatias que estouraram o teto de 100 mesmo em 1 dia + 1 tipo.</b> Maior que zero
    /// significa registros NÃO lidos. O detalhe de cada fatia vai para
    /// <see cref="SernitVarreduraFalha"/>.</summary>
    public int FatiasTruncadas { get; set; }

    // ---- Ponteiro de retomada ----
    public FaseVarreduraSernit Fase { get; set; } = FaseVarreduraSernit.Grade;
    public SituacaoSernit? CursorSituacao { get; set; }

    /// <summary>Data da Solicitação a partir da qual continuar (cursor do varredor por paginação —
    /// no SERNIT não há export; a leitura é página a página do datascroller).</summary>
    public DateOnly? CursorData { get; set; }

    /// <summary>Último <c>IdSernit</c> cujo histórico foi lido com sucesso (fase de histórico).</summary>
    public string? CursorIdSernit { get; set; }

    public int HistoricosPendentes { get; set; }
    public int Retomadas { get; set; }
    public DateTime? RetomadaEm { get; set; }

    /// <summary><b>Sinal de vida:</b> quando o motor gravou progresso pela última vez. Responde
    /// "está trabalhando ou pendurada?" sem ir ao journalctl.</summary>
    public DateTime? UltimoSinalEm { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
}
