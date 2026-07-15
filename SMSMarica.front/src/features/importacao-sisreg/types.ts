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
};
