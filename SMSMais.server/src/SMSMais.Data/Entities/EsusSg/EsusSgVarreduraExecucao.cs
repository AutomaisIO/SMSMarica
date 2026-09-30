using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.EsusSg;

/// <summary>Modo da rodada do motor do ESUS SG.</summary>
public enum ModoVarreduraEsusSg
{
    /// <summary><b>Carga inicial:</b> a fila inteira + os agendados de TODO o histórico, em fatias de um ano
    /// (medido: 4.924 exames agendados de 2019 a 2026).</summary>
    CargaInicial = 1,

    /// <summary><b>Diária:</b> a fila inteira + os agendados de uma janela móvel (passado recente e
    /// futuro), com diff de situação e detecção de quem saiu da fila.</summary>
    Diaria = 2,

    /// <summary><b>Só a fila:</b> atualiza posição, prioridade e pendência sem ler agendados.</summary>
    SomenteFila = 3,
}

/// <summary>Fase da rodada — o que retomar quando a execução volta.</summary>
public enum FaseVarreduraEsusSg
{
    Fila = 1,
    Agendados = 2,
    Finalizada = 3,
}

/// <summary>Estado de uma rodada. Mesmos números do SER/SERNIT.</summary>
public enum StatusVarreduraEsusSg
{
    Pendente = 1,
    EmExecucao = 2,
    Concluida = 3,

    /// <summary>Cobertura incompleta, declarada (uma fatia de agendados não fechou a conta
    /// lido = declarado, ou uma fila não pôde ser lida).</summary>
    Parcial = 4,

    Erro = 5,
    Cancelada = 6,

    /// <summary>Parada por queda do serviço, e RETOMÁVEL pelo cursor.</summary>
    Interrompida = 7,
}

/// <summary>
/// Uma execução do motor do ESUS de São Gonçalo (ADR-0063; irmã de <c>sernit_varredura_execucao</c>).
/// </summary>
public class EsusSgVarreduraExecucao
{
    public Guid Id { get; set; }

    public ModoVarreduraEsusSg Modo { get; set; } = ModoVarreduraEsusSg.Diaria;
    public DisparoSincronizacao Disparo { get; set; } = DisparoSincronizacao.Manual;
    public StatusVarreduraEsusSg Status { get; set; } = StatusVarreduraEsusSg.Pendente;

    /// <summary>Janela de DATA DO AGENDAMENTO lida na fase de agendados.</summary>
    public DateOnly JanelaInicio { get; set; }
    public DateOnly JanelaFim { get; set; }

    // ---- Contadores ----
    /// <summary>Requisições feitas ao ESUS (páginas de fila + páginas de agendados).</summary>
    public int Requisicoes { get; set; }
    public int NaFila { get; set; }
    public int AgendadosLidos { get; set; }
    public int SolicitacoesNovas { get; set; }
    public int SolicitacoesAtualizadas { get; set; }
    public int MudancasSituacao { get; set; }
    public int SaidasDaFila { get; set; }
    public int EventosNovos { get; set; }
    public int GatilhosGerados { get; set; }

    /// <summary>Meses de agendados que não fecharam lido = declarado. Maior que zero = cobertura
    /// incompleta; o detalhe vai para <see cref="EsusSgVarreduraFalha"/>.</summary>
    public int MesesIncompletos { get; set; }

    // ---- Ponteiro de retomada ----
    public FaseVarreduraEsusSg Fase { get; set; } = FaseVarreduraEsusSg.Fila;

    /// <summary>Até onde os agendados já foram lidos (marca de progresso; a retomada relê a janela).</summary>
    public DateOnly? CursorMes { get; set; }

    public int Retomadas { get; set; }
    public DateTime? RetomadaEm { get; set; }
    public DateTime? UltimoSinalEm { get; set; }

    public string? MensagemErro { get; set; }

    public DateTime IniciadoEm { get; set; }
    public DateTime? FinalizadoEm { get; set; }
    public int? DuracaoSegundos { get; set; }

    public Guid? CriadoPor { get; set; }
    public string? CriadoPorNome { get; set; }
}
