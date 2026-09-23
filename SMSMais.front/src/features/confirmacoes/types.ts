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

/** Um agendamento do paciente que ainda espera confirmação — para confirmar direto do chat (#133). */
export type AgendamentoPendentePaciente = {
  solicitacaoId: string;
  exameId: string | null;
  codigoSolicitacao: string | null;
  categoria: string;
  procedimento: string | null;
  unidadeExecutante: string | null;
  dataAgendada: string | null;
  emAtendimentoPorOutro: boolean;
  atendenteNome: string | null;
};

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
  /** O que a ficha do SISREG passou a dizer. Nulo quando não havia o que cancelar lá. */
  sisregSituacao: string | null;
  /** O aviso de cancelamento saiu na hora para o WhatsApp do paciente. */
  pacienteAvisado: boolean;
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

// ---- Aba Equipe (módulo ConfirmacoesEquipe) ----

/** A produção de uma atendente no período — contagem de ATOS dela, lidos da trilha. */
export type AtendenteProducao = {
  usuarioId: string;
  nome: string;
  pegou: number;
  confirmou: number;
  /** Cancelou a vaga AQUI. O que houve no SISREG está nas duas seguintes. */
  cancelou: number;
  cancelouNoSisreg: number;
  sisregRecusou: number;
  avisouPaciente: number;
  pendente: number;
  contatoErrado: number;
  contatoCorrigido: number;
  pedidoDesfeito: number;
  liberou: number;
  transferiu: number;
  desfechos: number;
  /** Mediana em minutos entre pegar a ficha e dar o desfecho. */
  tempoAteDesfechoMin: number | null;
  /** Mediana em minutos entre um desfecho e o seguinte (pausas longas não contam). */
  ritmoMin: number | null;
  diasAtivos: number;
  primeiraAcaoEm: string | null;
  ultimaAcaoEm: string | null;
};

export type EquipeDia = { dia: string; confirmou: number; cancelou: number; outros: number };

export type EquipeConfirmacoes = {
  de: string;
  ate: string;
  /** Primeiro evento da trilha — antes disso não há o que contar. */
  trilhaDesde: string | null;
  pausaMin: number;
  total: AtendenteProducao;
  atendentes: AtendenteProducao[];
  porDia: EquipeDia[];
};

export type AtoAtendente = {
  tipo: string;
  ocorridoEm: string;
  solicitacaoId: string;
  codigoSolicitacao: string | null;
  procedimento: string | null;
  observacao: string | null;
};
