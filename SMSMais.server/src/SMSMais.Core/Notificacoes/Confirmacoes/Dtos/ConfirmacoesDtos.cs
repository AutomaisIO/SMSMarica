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

/// <summary>Prévia (ou resultado) de um disparo em lote da confirmação.</summary>
public sealed record PreviaLoteConfirmacaoDto(
    /// <summary>Quantos seriam avisados com a régua atual.</summary>
    int Elegiveis,
    /// <summary>Agendamentos pendentes no período, antes dos filtros.</summary>
    int Candidatos,
    /// <summary>De fora: o procedimento está com o aviso desligado.</summary>
    int ForaProcedimentoDesligado,
    /// <summary>De fora: já existe comunicação de confirmação para o agendamento.</summary>
    int ForaJaAvisado,
    /// <summary>De fora: não veio do SISREG (cadastro manual).</summary>
    int ForaNaoSisreg,
    IReadOnlyList<LoteConfirmacaoPorDiaDto> PorDia,
    /// <summary>Preenchido só no disparo: quantos foram enfileirados.</summary>
    int? Enfileiradas,
    string? Aviso,
    /// <summary>Dos elegíveis, quantos são REENVIO para quem já tinha recebido (opção do lote).</summary>
    int ReenviosAvisados = 0,
    /// <summary>Dos elegíveis, quantos são reenvio para quem já tinha CONFIRMADO (opção do lote).</summary>
    int ReenviosConfirmados = 0);

public sealed record LoteConfirmacaoPorDiaDto(DateOnly Dia, int Total, int Consultas, int Exames);
