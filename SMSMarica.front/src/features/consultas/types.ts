export type ConsultaListItem = {
  id: string;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  categoria: string;
  especialidade: string | null;
  unidadeExecutanteNome: string;
  solicitanteNome: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  status: string;
  statusConfirmacao: string;
};

export type ConsultaDetalhe = {
  id: string;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  pacienteCns: string | null;
  categoria: string;
  especialidade: string | null;
  procedimentoTexto: string | null;
  procedimentoSigtapCodigo: string | null;
  unidadeExecutanteNome: string;
  unidadeSolicitanteNome: string | null;
  solicitanteNome: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  dataRegulacao: string | null;
  status: string;
  statusConfirmacao: string;
  observacoes: string | null;
};

export type FiltroConsultas = {
  pacienteId?: string;
  busca?: string;
  dataInicial?: string;
  dataFinal?: string;
  limite?: number;
};
