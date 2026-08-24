export type ImportacaoPreviewItem = {
  codigoSolicitacao: string;
  nomePaciente: string | null;
  cnsPaciente: string | null;
  procedimentoTexto: string | null;
  dataHoraAtendimento: string | null;
  nomeUnidadeSolicitante: string | null;
  cnesUnidadeSolicitante: string | null;
  nomeUnidadeExecutante: string | null;
  cnesUnidadeExecutante: string | null;
  nomeMedicoSolicitante: string | null;
  jaExiste: boolean;
  unidadeSolicitanteExiste: boolean;
  unidadeExecutanteExiste: boolean;
  procedimentoMapeia: boolean;
  alertas: string[];
};

export type ImportacaoExecucaoResultado = {
  codigoSolicitacao: string;
  sucesso: boolean;
  solicitacaoId: string | null;
  accessionNumber: string | null;
  pacienteNome: string | null;
  pacienteCriado: boolean;
  unidadeSolicitanteCriada: boolean;
  unidadeExecutanteCriada: boolean;
  passos: string[];
  erro: string | null;
};

export type ImportacaoPreviewResultado = {
  inicio: string;
  fim: string;
  total: number;
  novos: number;
  existentes: number;
  itens: ImportacaoPreviewItem[];
  /** Linhas que o parser recusou — já gravadas na lista de erros. */
  rejeitadas: number;
};

/** Por que a linha não virou solicitação. `Arquivo` = o .txt/.csv inteiro não é do SISREG. */
export type OrigemFalhaImportacao = 'Parser' | 'Execucao' | 'Arquivo';

/** Uma linha do SISREG que não virou solicitação (guardada com o RAW, para revalidar). */
export type ImportacaoFalha = {
  id: string;
  codigoSolicitacao: string | null;
  origem: OrigemFalhaImportacao;
  motivo: string;
  linhaRaw: string;
  nomeArquivo: string | null;
  nomePaciente: string | null;
  procedimentoTexto: string | null;
  dataAgendada: string | null;
  nomeExecutante: string | null;
  tentativas: number;
  criadoEm: string;
  atualizadoEm: string;
  resolvidoEm: string | null;
  resolucaoNota: string | null;
  solicitacaoId: string | null;
  /** Causa tipada — decide qual ação a tela oferece nesta linha (ADR-0035). */
  causa: CausaFalhaImportacao;
  pacienteCns: string | null;
  /** Só a causa `CpfNaoResolvido` é resolvível informando o CPF. */
  podeInformarCpf: boolean;
};

export type CausaFalhaImportacao =
  | 'SemCns'
  | 'CadsusIndisponivel'
  | 'CpfNaoResolvido'
  | 'UnidadeNaoResolvida'
  | 'LinhaInvalida'
  | 'ArquivoIncompativel'
  | 'Outro';

export type ImportacaoFalhaReprocessoResultado = {
  falhaId: string;
  /** True = saiu da lista de pendências (importou agora ou já existia). */
  resolvida: boolean;
  execucao: ImportacaoExecucaoResultado | null;
  mensagem: string;
};

/** Um campo do SISREG já legível, para o modal exibir ao lado do RAW. */
export type CampoSisreg = {
  coluna: number;
  rotulo: string;
  valor: string | null;
};

/** Detalhe da falha: o RAW guardado + o parse dele (campos nomeados + unidades). */
export type ImportacaoFalhaDetalhe = {
  falha: ImportacaoFalha;
  /** False para arquivo incompatível / linha ilegível — aí só há o RAW. */
  parseavel: boolean;
  campos: CampoSisreg[];
  nomeUnidadeSolicitante: string | null;
  cnesUnidadeSolicitante: string | null;
  nomeUnidadeExecutante: string | null;
  cnesUnidadeExecutante: string | null;
};

export type StatusImportacaoArquivo =
  | 'Pendente'
  | 'EmExecucao'
  | 'Concluida'
  | 'ArquivoIncompativel'
  | 'Erro'
  | 'Cancelada';

/** Uma linha da aba de rastreio: um arquivo importado. */
export type ImportacaoExecucao = {
  id: string;
  loteId: string;
  nomeArquivo: string;
  caminhoNoZip: string | null;
  status: StatusImportacaoArquivo;
  totalRegistros: number;
  validos: number;
  invalidos: number;
  jaExistiam: number;
  mensagem: string | null;
  iniciadoEm: string;
  concluidoEm: string | null;
  criadoPorNome: string | null;
};

/** Progresso do lote em andamento (ou resumo do último). Null quando nunca houve importação. */
export type StatusLote = {
  loteId: string;
  emExecucao: boolean;
  totalArquivos: number;
  arquivosFeitos: number;
  arquivoAtual: string | null;
  validos: number;
  invalidos: number;
};

/** Resposta do envio do lote: o que entrou na fila e o que foi ignorado pela extensão. */
export type ImportacaoLoteAceito = {
  loteId: string;
  arquivosAceitos: number;
  /** Ignorados por extensão (não .txt/.csv) — nem foram lidos, não viram erro. */
  arquivosIgnorados: string[];
};

/**
 * Pendências de SIGTAP agrupadas por procedimento. Uma varredura sem mapeamento gera uma pendência
 * por solicitação — todas com a mesma causa e a mesma correção.
 */
export type PendenciaSigtapAgrupada = {
  procedimentoTexto: string;
  /** O código do procedimento no SISREG (o `pa`). */
  codigoSisreg: string | null;
  /** Linha do catálogo a mapear. Null = código ainda não catalogado (rode "Atualizar mapeamento"). */
  deParaId: string | null;
  solicitacoes: number;
  primeiraEm: string;
  ultimaEm: string;
};

export type ReprocessoLoteResultado = {
  total: number;
  importadas: number;
  /** Continuam pendentes por OUTRO motivo (paciente sem CNS, CPF não resolvido…). */
  continuam: number;
  mensagem: string;
};
