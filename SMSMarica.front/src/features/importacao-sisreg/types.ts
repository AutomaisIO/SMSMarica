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

/** Por que a linha não virou solicitação. */
export type OrigemFalhaImportacao = 'Parser' | 'Execucao';

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
};

export type ImportacaoFalhaReprocessoResultado = {
  falhaId: string;
  /** True = saiu da lista de pendências (importou agora ou já existia). */
  resolvida: boolean;
  execucao: ImportacaoExecucaoResultado | null;
  mensagem: string;
};
