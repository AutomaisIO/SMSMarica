import type {
  NotificacaoResumo,
  StatusConfirmacao,
} from '@/features/notificacoes-agendamento/types';

export type ConfirmacaoConfiguracao = {
  /** "HH:mm" — Brasília. */
  horaInicioEnvio: string;
  horaFimEnvio: string;
  maximoPorPassagem: number;
  somenteSisreg: boolean;
  janelaAbertaAgora: boolean;
  atualizadoEm: string | null;
};

export type SalvarConfirmacaoConfiguracao = Pick<
  ConfirmacaoConfiguracao,
  'horaInicioEnvio' | 'horaFimEnvio' | 'maximoPorPassagem' | 'somenteSisreg'
>;

export type ResumoFilaConfirmacao = {
  janelaAbertaAgora: boolean;
  horaInicioEnvio: string;
  horaFimEnvio: string;
  proximaAberturaEm: string;
  naFila: number;
  prontasParaSair: number;
  aguardandoVerificacaoCadastral: number;
  numeroInvalido: number;
  semTelefoneValido: number;
  falha: number;
  enviadasHoje: number;
  confirmadasHoje: number;
  canceladasHoje: number;
};

export type FiltroFila = {
  status?: string;
  confirmacao?: string;
  texto?: string;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
};

export type PaginaFila = {
  itens: NotificacaoResumo[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type RespostaConfirmacao = {
  solicitacaoId: string;
  exameId: string | null;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  categoria: string;
  procedimento: string | null;
  unidadeExecutante: string | null;
  dataAgendada: string | null;
  statusConfirmacao: StatusConfirmacao;
  canal: string | null;
  respondidoEm: string | null;
  motivo: string | null;
  statusSolicitacao: string;
};

export type FiltroRespostas = {
  resposta?: string;
  de?: string;
  ate?: string;
  texto?: string;
  pagina?: number;
  tamanho?: number;
};

export type PaginaRespostas = {
  itens: RespostaConfirmacao[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type RegraUnidade = {
  unidadeId: string;
  unidadeNome: string;
  enviarConfirmacao: boolean;
  procedimentosComAviso: number;
  procedimentosTotal: number;
};
