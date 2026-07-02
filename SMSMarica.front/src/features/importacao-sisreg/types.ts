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

export type ImportacaoPreviewResultado = {
  inicio: string;
  fim: string;
  total: number;
  novos: number;
  existentes: number;
  itens: ImportacaoPreviewItem[];
};
