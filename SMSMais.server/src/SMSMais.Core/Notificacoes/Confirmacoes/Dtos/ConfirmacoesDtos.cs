namespace SMSMais.Core.Notificacoes.Confirmacoes.Dtos;

/// <summary>Regras de disparo. Horários "HH:mm" (Brasília).</summary>
public sealed record ConfirmacaoConfiguracaoDto(
    string HoraInicioEnvio,
    string HoraFimEnvio,
    int MaximoPorPassagem,
    bool SomenteSisreg,
    bool JanelaAbertaAgora,
    DateTime? AtualizadoEm);

public sealed record SalvarConfirmacaoConfiguracaoRequest(
    string HoraInicioEnvio,
    string HoraFimEnvio,
    int MaximoPorPassagem,
    bool SomenteSisreg);

/// <summary>Fotografia da fila de confirmações (só a finalidade confirmação de agendamento).</summary>
public sealed record ResumoFilaConfirmacaoDto(
    bool JanelaAbertaAgora,
    string HoraInicioEnvio,
    string HoraFimEnvio,
    DateTime ProximaAberturaEm,
    /// <summary>Na fila para sair (Pendente com próxima tentativa marcada).</summary>
    int NaFila,
    /// <summary>Da fila, quantas já venceram e só esperam a janela/worker.</summary>
    int ProntasParaSair,
    int AguardandoVerificacaoCadastral,
    int NumeroInvalido,
    int SemTelefoneValido,
    int Falha,
    int EnviadasHoje,
    int ConfirmadasHoje,
    int CanceladasHoje);

/// <summary>Resposta do paciente a uma confirmação (confirmou ou avisou que não vai).</summary>
public sealed record RespostaConfirmacaoDto(
    Guid SolicitacaoId,
    Guid? ExameId,
    string? CodigoSolicitacao,
    Guid PacienteId,
    string? PacienteNome,
    string Categoria,
    string? Procedimento,
    string? UnidadeExecutante,
    DateTime? DataAgendada,
    string StatusConfirmacao,
    string? Canal,
    DateTime? RespondidoEm,
    string? Motivo,
    string StatusSolicitacao);

public sealed record PaginaRespostasConfirmacaoDto(
    IReadOnlyList<RespostaConfirmacaoDto> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Chave "avisar por WhatsApp" de uma unidade (regra por unidade).</summary>
public sealed record RegraUnidadeConfirmacaoDto(
    Guid UnidadeId,
    string UnidadeNome,
    bool EnviarConfirmacao,
    int ProcedimentosComAviso,
    int ProcedimentosTotal);
