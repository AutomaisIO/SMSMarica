using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

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
    string? Erro,
    /// <summary>Causa tipada do impedimento — é o que a lista de pendências grava para saber qual
    /// ação oferecer. <c>null</c> quando houve sucesso. Ver ADR-0035.</summary>
    CausaFalhaImportacao? Causa = null);

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
/// <summary>
/// Pendências de SIGTAP agrupadas por PROCEDIMENTO. Uma varredura de mamografia com 200
/// agendamentos e sem mapeamento gera 200 pendências — todas com a mesma causa e a mesma
/// correção. Listadas uma a uma, inundam a aba de Erros e escondem as pendências que são
/// realmente individuais (paciente sem CNS, CPF não resolvido).
/// </summary>
/// <param name="CodigoSisreg">O <c>pa</c> — pode variar entre as pendências do mesmo nome quando
/// a varredura passou por um "GRUPO -" e pelo item individual.</param>
public sealed record PendenciaSigtapAgrupadaDto(
    string ProcedimentoTexto,
    string? CodigoSisreg,
    /// <summary>Linha do catálogo a mapear. NULL quando o código não foi catalogado — nesse caso
    /// a correção é rodar "Atualizar mapeamento" na unidade antes.</summary>
    Guid? DeParaId,
    int Solicitacoes,
    DateTime PrimeiraEm,
    DateTime UltimaEm);

/// <summary>Qual procedimento revalidar em lote.</summary>
public sealed record ReprocessarSigtapRequest(string ProcedimentoTexto);

/// <summary>Resultado de revalidar em lote as pendências de um procedimento.</summary>
public sealed record ReprocessoLoteResultado(
    int Total,
    int Importadas,
    int Continuam,
    string Mensagem);

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
    Guid? SolicitacaoId,
    /// <summary>Causa tipada — decide a ação que a tela oferece nesta linha.</summary>
    CausaFalhaImportacao Causa = CausaFalhaImportacao.Outro,
    string? PacienteCns = null)
{
    /// <summary>
    /// A linha é resolvível informando o CPF? Só a causa <c>CpfNaoResolvido</c> — as demais ou são
    /// transitórias (revalidar basta) ou não têm o dado na origem. A tela usa isto para não oferecer
    /// um botão que fatalmente falharia.
    /// </summary>
    public bool PodeInformarCpf =>
        ResolvidoEm is null && Causa == CausaFalhaImportacao.CpfNaoResolvido;
}

/// <summary>Uma linha da aba de rastreio: um arquivo importado.</summary>
public sealed record ImportacaoExecucaoDto(
    Guid Id,
    Guid LoteId,
    string NomeArquivo,
    string? CaminhoNoZip,
    StatusImportacaoArquivo Status,
    int TotalRegistros,
    int Validos,
    int Invalidos,
    int JaExistiam,
    string? Mensagem,
    DateTime IniciadoEm,
    DateTime? ConcluidoEm,
    string? CriadoPorNome);

/// <summary>Resposta do envio do lote: o que entrou na fila e o que foi ignorado pela extensão.</summary>
public sealed record ImportacaoLoteAceitoDto(
    Guid LoteId,
    int ArquivosAceitos,
    /// <summary>Ignorados por extensão (não .txt/.csv) — nem foram lidos, não viram erro.</summary>
    IReadOnlyList<string> ArquivosIgnorados);

/// <summary>Contadores de UM arquivo processado no lote.</summary>
public sealed record ResultadoArquivoImportado(
    int Total,
    /// <summary>Entraram agora ou já existiam — o arquivo está honrado.</summary>
    int Validos,
    int Invalidos,
    /// <summary>Subconjunto de <see cref="Validos"/> que já estava no sistema.</summary>
    int JaExistiam,
    /// <summary>True = é .txt/.csv mas não é do SISREG; nenhuma linha foi tentada.</summary>
    bool Incompativel,
    string? Motivo);

/// <summary>Um campo do SISREG já legível, para o modal exibir ao lado do RAW.</summary>
public sealed record CampoSisreg(int Coluna, string Rotulo, string? Valor);

/// <summary>
/// Detalhe da falha para análise: o RAW guardado E o parse dele lado a lado. Os campos vêm de
/// REPARSEAR o RAW na hora — não há colunas espelho no banco, então o que o modal mostra é sempre
/// o que o parser atual entende, sem risco de divergir da verdade.
/// </summary>
public sealed record ImportacaoFalhaDetalheDto(
    ImportacaoFalhaDto Falha,
    /// <summary>False para falhas de arquivo incompatível/linha ilegível — aí só há o RAW.</summary>
    bool Parseavel,
    /// <summary>Campos nomeados do layout de 38 colunas (vazio quando não parseável).</summary>
    IReadOnlyList<CampoSisreg> Campos,
    string? NomeUnidadeSolicitante,
    string? CnesUnidadeSolicitante,
    string? NomeUnidadeExecutante,
    string? CnesUnidadeExecutante);

/// <summary>Resultado de "Validar" (reprocessar) uma falha a partir do RAW guardado.</summary>
public sealed record ImportacaoFalhaReprocessoResultado(
    Guid FalhaId,
    /// <summary>True = a linha saiu da lista de pendências (importou agora ou já existia).</summary>
    bool Resolvida,
    /// <summary>Execução completa quando a linha pôde ser reprocessada; null quando nem parseou.</summary>
    ImportacaoExecucaoResultado? Execucao,
    /// <summary>O que aconteceu, em uma frase (para o operador).</summary>
    string Mensagem);
