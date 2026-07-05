export type StatusNotificacao =
  | 'Pendente'
  | 'Enviada'
  | 'Entregue'
  | 'Lida'
  | 'Falha'
  | 'SemTelefoneValido';

export type StatusConfirmacao = 'Pendente' | 'Confirmada' | 'Cancelada';

export type FinalidadeComunicacao = 'ConfirmacaoAgendamento' | 'ExameLiberado' | 'LaudoPronto';

export type NotificacaoFiltro = {
  status?: string;
  finalidade?: string;
  confirmacao?: string;
  texto?: string;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
};

export type NotificacaoResumo = {
  id: string;
  finalidade: FinalidadeComunicacao;
  solicitacaoExameId: string | null;
  accessionNumber: string | null;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  tipoExameNome: string | null;
  unidadeNome: string | null;
  dataAgendada: string | null;
  telefone: string | null;
  status: StatusNotificacao;
  motivoFalha: string | null;
  tentativas: number;
  enviadoEm: string | null;
  entregueEm: string | null;
  lidoEm: string | null;
  visualizadoEm: string | null;
  statusConfirmacao: StatusConfirmacao;
  confirmadoEm: string | null;
  confirmadoCanal: string | null;
  motivoCancelamentoPaciente: string | null;
  criadoEm: string;
};

export type PaginaNotificacoes = {
  itens: NotificacaoResumo[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type NotificacaoDetalhe = {
  resumo: NotificacaoResumo;
  ultimaTentativaEm: string | null;
  proximaTentativaEm: string | null;
  mensagemConteudo: string | null;
  mensagemStatus: string | null;
  mensagemErroMeta: string | null;
  linkExpiraEm: string | null;
  linkUsadoEm: string | null;
  linkUsadoIp: string | null;
};
