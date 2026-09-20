import type { StatusConfirmacao, StatusNotificacao } from '@/features/mensageria/types';

export type AbaAtendimento =
  | 'NaoConfirmados'
  | 'Confirmados'
  | 'ContatoErrado'
  | 'Pendentes'
  | 'TelefoneComprometido'
  | 'Cancelamento';

/** Por que o canal não alcança o paciente. */
export type MotivoTelefoneComprometido = 'SemCelular' | 'NaoEhWhatsApp';

export type SituacaoAtendimento =
  | 'EmAtendimento'
  | 'Confirmado'
  | 'Cancelado'
  | 'Pendente'
  | 'ContatoErrado'
  | 'Liberado'
  | 'ContatoCorrigido';

export type EnvioConfirmacao = {
  comunicacaoId: string;
  status: StatusNotificacao;
  motivoFalha: string | null;
  erroMeta: string | null;
  tentativas: number;
  proximaTentativaEm: string | null;
  enviadoEm: string | null;
  entregueEm: string | null;
  lidoEm: string | null;
  visualizadoEm: string | null;
  telefone: string | null;
};

export type Atendimento = {
  id: string;
  atendenteId: string;
  atendenteNome: string;
  situacao: SituacaoAtendimento;
  motivo: string | null;
  iniciadoEm: string;
  atualizadoEm: string | null;
  ehMeu: boolean;
};

export type SolicitacaoAtendimento = {
  solicitacaoId: string;
  exameId: string | null;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  telefone: string | null;
  telefoneVerificado: boolean;
  categoria: string;
  procedimento: string | null;
  unidadeExecutanteId: string;
  unidadeExecutante: string | null;
  dataAgendada: string | null;
  statusConfirmacao: StatusConfirmacao;
  confirmadoCanal: string | null;
  respondidoEm: string | null;
  motivoCancelamentoPaciente: string | null;
  envio: EnvioConfirmacao | null;
  janelaZapAberta: boolean;
  conversaId: string | null;
  contatoNegado: boolean;
  atendimento: Atendimento | null;
  /** Preenchido quando há marca aberta de contato que o canal não alcança. */
  motivoTelefoneComprometido: MotivoTelefoneComprometido | null;
  /** Mensagens já perdidas por esse mesmo motivo. */
  tentativasPerdidas: number;
};

export type PaginaAtendimento = {
  itens: SolicitacaoAtendimento[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ResumoAbas = {
  naoConfirmados: number;
  confirmados: number;
  contatoErrado: number;
  pendentes: number;
  emAtendimentoComigo: number;
  telefoneComprometido: number;
  cancelamento: number;
};

/** Uma mensagem da conversa, para ler o contexto antes de cancelar. */
export type MensagemContexto = {
  doPaciente: boolean;
  texto: string | null;
  template: string | null;
  ocorridoEm: string;
  autor: string | null;
};

export type MotivosTelefoneComprometido = {
  semCelular: number;
  naoEhWhatsApp: number;
  total: number;
  pacientesDistintos: number;
};

export type AtendenteConfirmacao = { id: string; nome: string };

export type FiltroAtendimento = {
  aba: AbaAtendimento;
  texto?: string;
  unidadeId?: string;
  envio?: string;
  pagina?: number;
  tamanho?: number;
};

export type AcaoResultado = {
  atendimentoId: string;
  situacao: SituacaoAtendimento;
  orientacaoSisreg: boolean;
};

export type EventoAtendimento = {
  tipo: string;
  atorUsuarioId: string | null;
  atorNome: string | null;
  deUsuarioId: string | null;
  deNome: string | null;
  paraUsuarioId: string | null;
  paraNome: string | null;
  observacao: string | null;
  ocorridoEm: string;
};
