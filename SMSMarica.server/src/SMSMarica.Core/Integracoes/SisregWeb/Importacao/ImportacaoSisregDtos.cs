using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>Uma marcação do SISREG confrontada com a nossa base (o "diff" da importação).</summary>
public sealed record ImportacaoPreviewItem(
    string CodigoSolicitacao,
    string? NomePaciente,
    string? CnsPaciente,
    string? ProcedimentoTexto,
    DateTime? DataHoraAtendimento,
    string? NomeUnidadeSolicitante,
    string? CnesUnidadeSolicitante,
    string? NomeUnidadeExecutante,
    string? CnesUnidadeExecutante,
    string? NomeMedicoSolicitante,
    /// <summary>True se já existe uma solicitação ativa com esse código (nº SISREG).</summary>
    bool JaExiste,
    /// <summary>Unidade solicitante já cadastrada (por CNES).</summary>
    bool UnidadeSolicitanteExiste,
    /// <summary>Unidade executante já cadastrada (por CNES).</summary>
    bool UnidadeExecutanteExiste,
    /// <summary>Procedimento (texto) mapeia para um tipo de exame nosso.</summary>
    bool ProcedimentoMapeia,
    /// <summary>Impedimentos que fariam esse item cair em divergência (vazio = importável).</summary>
    IReadOnlyList<string> Alertas);

/// <summary>Resultado de importar UMA marcação (executar o fluxo inteiro de um registro).</summary>
public sealed record ImportacaoExecucaoResultado(
    string CodigoSolicitacao,
    bool Sucesso,
    /// <summary>Id da SolicitacaoExame criada (null se não criou).</summary>
    Guid? SolicitacaoId,
    string? AccessionNumber,
    /// <summary>Nome do paciente resolvido/criado.</summary>
    string? PacienteNome,
    bool PacienteCriado,
    bool UnidadeSolicitanteCriada,
    /// <summary>Unidade executante foi criada agora (não existia por CNES nem por nome).</summary>
    bool UnidadeExecutanteCriada,
    /// <summary>Passos executados, em ordem (para o operador conferir o fluxo).</summary>
    IReadOnlyList<string> Passos,
    /// <summary>Mensagem de erro/impedimento quando Sucesso=false.</summary>
    string? Erro);

/// <summary>Resultado do preview de importação para um período.</summary>
public sealed record ImportacaoPreviewResultado(
    DateOnly Inicio,
    DateOnly Fim,
    int Total,
    int Novos,
    int Existentes,
    IReadOnlyList<ImportacaoPreviewItem> Itens,
    /// <summary>Linhas do arquivo que o parser rejeitou (já gravadas na lista de erros).</summary>
    int Rejeitadas);

/// <summary>Uma linha do SISREG que não virou solicitação, como aparece na lista de erros.</summary>
public sealed record ImportacaoFalhaDto(
    Guid Id,
    string? CodigoSolicitacao,
    OrigemFalhaImportacao Origem,
    string Motivo,
    string LinhaRaw,
    string? NomeArquivo,
    string? NomePaciente,
    string? ProcedimentoTexto,
    DateTime? DataAgendada,
    string? NomeExecutante,
    int Tentativas,
    DateTime CriadoEm,
    DateTime AtualizadoEm,
    DateTime? ResolvidoEm,
    string? ResolucaoNota,
    Guid? SolicitacaoId);

/// <summary>Resultado de "Validar" (reprocessar) uma falha a partir do RAW guardado.</summary>
public sealed record ImportacaoFalhaReprocessoResultado(
    Guid FalhaId,
    /// <summary>True = a linha saiu da lista de pendências (importou agora ou já existia).</summary>
    bool Resolvida,
    /// <summary>Execução completa quando a linha pôde ser reprocessada; null quando nem parseou.</summary>
    ImportacaoExecucaoResultado? Execucao,
    /// <summary>O que aconteceu, em uma frase (para o operador).</summary>
    string Mensagem);
