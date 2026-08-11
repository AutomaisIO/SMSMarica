using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura.Dtos;

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
    /// <summary>Janela em que o motor pode rodar (hora de Brasília) — fora dela a varredura
    /// derrubaria a sessão do atendente da unidade.</summary>
    TimeOnly JanelaInicioLocal,
    TimeOnly JanelaFimLocal,
    /// <summary>Gatilho mestre da unidade: importar solicitação avisa o paciente por WhatsApp?
    /// Vale para toda importação — varredura e upload de arquivo.</summary>
    bool EnviarConfirmacao);

/// <summary>Ligar/desligar o sincronismo diário e ajustar hora e janela de dias.</summary>
public sealed record SalvarVarreduraAgendaRequest(
    bool Ativo,
    TimeOnly HoraLocal,
    int DiasAFrente,
    /// <summary>Omitido mantém o valor atual — a tela pode salvar só a agenda sem mexer no
    /// gatilho de confirmação, e vice-versa.</summary>
    bool? EnviarConfirmacao = null);

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
