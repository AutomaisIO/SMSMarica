using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura.Dtos;

/// <summary>Agenda do motor diário de uma unidade, com o custo estimado da próxima varredura.</summary>
public sealed record VarreduraAgendaDto(
    Guid UnidadeId,
    string UnidadeNome,
    bool Ativo,
    TimeOnly HoraLocal,
    int DiasAFrente,
    DateTime? ProximoRunEm,
    DateTime? PausadoAte,
    DateTime? UltimaExecucaoEm,
    int FalhasConsecutivas,
    /// <summary>Pares profissional × procedimento habilitados — o que será varrido. O SIGTAP não
    /// entra aqui: ele é resolvido na importação, a partir do procedimento de cada agendamento.</summary>
    int CombinacoesProntas,
    /// <summary>Estimativa de requisições da próxima varredura, para comparar com o teto.</summary>
    int RequisicoesEstimadas,
    int TetoPorExecucao,
    /// <summary>Faixa (hora de Brasília) em que o SISREG bloqueia a exportação da agenda: a varredura
    /// não roda entre <see cref="CorteEntradaLocal"/> e <see cref="BloqueioFimLocal"/>; fora disso,
    /// qualquer hora.</summary>
    TimeOnly BloqueioInicioLocal,
    TimeOnly BloqueioFimLocal,
    /// <summary>Hora a partir da qual já não se pode INICIAR uma varredura (bloqueio − margem).</summary>
    TimeOnly CorteEntradaLocal,
    /// <summary>Gatilho mestre da unidade: importar solicitação avisa o paciente por WhatsApp?
    /// Vale para toda importação — varredura e upload de arquivo.</summary>
    bool EnviarConfirmacao,
    /// <summary>Puxar a agenda da unidade inteira numa requisição, em vez de uma por par
    /// profissional × procedimento. Ver <c>SisregVarreduraAgenda.RecorteUnidadeInteira</c>.</summary>
    bool RecorteUnidadeInteira = false);

/// <summary>Ligar/desligar o sincronismo diário e ajustar hora e janela de dias.</summary>
public sealed record SalvarVarreduraAgendaRequest(
    bool Ativo,
    TimeOnly HoraLocal,
    int DiasAFrente,
    /// <summary>Omitido mantém o valor atual — a tela pode salvar só a agenda sem mexer no
    /// gatilho de confirmação, e vice-versa.</summary>
    bool? EnviarConfirmacao = null,
    /// <summary>
    /// OBSOLETO na prática: o recorte por unidade inteira virou regra e não se desliga mais pela
    /// tela. Só <c>true</c> tem efeito (liga); <c>false</c> e omitido são ignorados.
    /// </summary>
    bool? RecorteUnidadeInteira = null);

/// <summary>Uma execução do motor, para a lista de "varreduras recentes".</summary>
public sealed record VarreduraExecucaoDto(
    Guid Id,
    Guid UnidadeId,
    string UnidadeNome,
    DisparoSincronizacao Disparo,
    StatusVarredura Status,
    DateOnly JanelaInicio,
    DateOnly JanelaFim,
    int CombinacoesTotal,
    int CombinacoesFeitas,
    int Requisicoes,
    int RegistrosEncontrados,
    int Validos,
    int Invalidos,
    int JaExistiam,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos,
    string? CriadoPorNome);

/// <summary>Varredura aceita e enfileirada.</summary>
public sealed record VarreduraAceitaDto(Guid ExecucaoId, string Mensagem);

/// <summary>
/// Dispara uma varredura MANUAL por período específico (inclusive datas passadas). Diferente do
/// "sincronizar agora", que varre de hoje até hoje + dias à frente. Não avisa o paciente por
/// WhatsApp — é backfill.
/// </summary>
public sealed record IniciarVarreduraPeriodoRequest(DateOnly DataInicio, DateOnly DataFim);

/// <summary>Detalhe de UMA combinação profissional × procedimento dentro de uma varredura — o que
/// o modal "detalhes da sincronização" lista. Sem CPF: só o nome do profissional.</summary>
public sealed record VarreduraExecucaoItemDto(
    Guid Id,
    string ProfissionalNome,
    string ProcedimentoCodigo,
    string ProcedimentoNome,
    int Requisicoes,
    int RegistrosEncontrados,
    int Validos,
    int Invalidos,
    int JaExistiam,
    string? Observacao);
