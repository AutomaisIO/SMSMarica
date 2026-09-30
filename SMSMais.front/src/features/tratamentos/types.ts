import type { Parentesco } from '@/features/acompanhantes/types';

export const STATUS_SESSAO = [
  'Pendente',
  'Confirmada',
  'Realizada',
  'Cancelada',
  'NaoRealizada',
  'AguardandoRetorno',
] as const;
export type StatusSessao = (typeof STATUS_SESSAO)[number];

export const STATUS_SESSAO_VALOR: Record<StatusSessao, number> = {
  Pendente: 1,
  Confirmada: 2,
  Realizada: 3,
  Cancelada: 4,
  NaoRealizada: 5,
  AguardandoRetorno: 6,
};

export function statusSessaoDeNumero(n: number | StatusSessao): StatusSessao {
  if (typeof n === 'string') return n;
  const entry = Object.entries(STATUS_SESSAO_VALOR).find(([, v]) => v === n);
  return (entry?.[0] as StatusSessao) ?? 'Pendente';
}

// ---- Condição do paciente para a viagem

export const MOBILIDADES = [
  'Independente',
  'CadeiranteTransfereParaBanco',
  'CadeiranteVeiculoAdaptado',
  'Maca',
] as const;
export type Mobilidade = (typeof MOBILIDADES)[number];

export const ROTULO_MOBILIDADE: Record<Mobilidade, string> = {
  Independente: 'Anda e senta sem ajuda especial',
  CadeiranteTransfereParaBanco: 'Cadeirante — passa para o banco (a cadeira dobra e vai guardada)',
  CadeiranteVeiculoAdaptado: 'Cadeirante — viaja na cadeira (só em veículo adaptado)',
  Maca: 'Viaja deitado (maca)',
};

export type Necessidades = {
  mobilidade: Mobilidade;
  dificuldadeVeiculoAlto: boolean;
  /** Imunodeficiente: viaja só com o próprio acompanhante (veículo exclusivo). */
  isolamento: boolean;
  usaOxigenio: boolean;
  necessitaAjuda: boolean;
  ajudaDescricao: string | null;
};

export const SEM_NECESSIDADES: Necessidades = {
  mobilidade: 'Independente',
  dificuldadeVeiculoAlto: false,
  isolamento: false,
  usaOxigenio: false,
  necessitaAjuda: false,
  ajudaDescricao: null,
};

// ---- Agenda: dias da semana + N sessões ou contínuo

export type AgendaPayload = {
  dataInicio: string;
  /** bit 0 = domingo … bit 6 = sábado */
  diasSemanaMascara: number;
  quantidadeSessoes: number | null;
  continuo: boolean;
};

export type Agenda = AgendaPayload & {
  /** Até quando as sessões já foram geradas (no contínuo, o horizonte que a renovação estende). */
  sessoesGeradasAte: string | null;
};

export type PreviaAgenda = {
  datas: string[];
  geradasAte: string | null;
};

// ---- Acompanhantes: 1 por direito, 2 com liberação

export type RegraAcompanhantesPayload = {
  quantidade: 1 | 2;
  justificativaSegundo: string | null;
};

export type RegraAcompanhantes = {
  quantidade: number;
  justificativaSegundo: string | null;
  liberadoPorNome: string | null;
  liberadoEm: string | null;
};

export type AcompanhanteDaSessao = {
  id: string;
  nome: string;
  parentesco: Parentesco | null;
};

export type TipoTratamento = {
  id: string;
  nome: string;
  codigo: string;
  tempoMedioMinutos: number | null;
  ativo: boolean;
};

export type TratamentoListItem = {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  tipoTratamentoNome: string | null;
  descricao: string;
  /** Do tipo de tratamento. */
  tempoMedioMinutos: number | null;
  diasSemanaMascara: number;
  continuo: boolean;
  proximaSessao: string | null;
  totalSessoes: number;
  sessoesRealizadas: number;
  ativo: boolean;
};

export type Sessao = {
  id: string;
  tratamentoId: string;
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
  status: StatusSessao | number;
  realizadaEm: string | null;
  /** Texto livre de antes da lista de acompanhantes — só histórico. */
  nomeAcompanhante: string | null;
  parentescoAcompanhante: string | null;
  acompanhantes: AcompanhanteDaSessao[];
  motoristaIdaId: string | null;
  veiculoIdaId: string | null;
  horaSaidaResidencia: string | null;
  horaChegadaUnidade: string | null;
  motoristaVoltaId: string | null;
  veiculoVoltaId: string | null;
  horaSaidaUnidade: string | null;
  horaChegadaResidencia: string | null;
  motivoNaoRealizacao: string | null;
  observacoes: string | null;
  alocadaEmRotaId: string | null;
  alocadaNaData: string | null;
  fileiraAssentoAlocado: number | null;
  numeroAssentoAlocado: number | null;
};

export type Tratamento = {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  unidadeAtendimentoId: string;
  unidadeAtendimentoNome: string;
  unidadeAtendimentoCidade: string | null;
  tipoTratamentoId: string | null;
  tipoTratamentoNome: string | null;
  /** Do tipo de tratamento. */
  tempoMedioMinutos: number | null;
  descricao: string;
  observacoes: string | null;
  agenda: Agenda;
  necessidades: Necessidades;
  acompanhantes: RegraAcompanhantes;
  ativo: boolean;
  criadoEm: string;
  encerradoEm: string | null;
  sessoes: Sessao[];
};

/** Destino disponível no seletor do atendimento (unidades de atendimento ativas). */
export type UnidadeAtendimentoOpcao = {
  id: string;
  nome: string;
  bairro: string | null;
  cidade: string | null;
  uf: string | null;
  temCoordenada: boolean;
};

export type CadastrarTratamentoPayload = {
  pacienteId: string;
  unidadeAtendimentoId: string;
  tipoTratamentoId: string;
  descricao: string;
  observacoes: string | null;
  agenda: AgendaPayload;
  necessidades: Necessidades;
  acompanhantes: RegraAcompanhantesPayload;
};

export type AtualizarTratamentoPayload = {
  descricao: string;
  unidadeAtendimentoId: string;
  tipoTratamentoId: string;
  observacoes: string | null;
  necessidades: Necessidades;
  acompanhantes: RegraAcompanhantesPayload;
};

export type AdicionarSessaoPayload = {
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
};

export type AtualizarSessaoPayload = {
  dataPrevista: string;
  horaPrevistaBusca: string | null;
  horaPrevistaRetorno: string | null;
  observacoes: string | null;
};

export type ConfirmarSessaoPayload = {
  realizada: boolean;
  /** Quem acompanhou de fato; null = mantém a escolha feita antes da viagem. */
  acompanhanteIds: string[] | null;
  motoristaIdaId: string | null;
  veiculoIdaId: string | null;
  horaSaidaResidencia: string | null;
  horaChegadaUnidade: string | null;
  motoristaVoltaId: string | null;
  veiculoVoltaId: string | null;
  horaSaidaUnidade: string | null;
  horaChegadaResidencia: string | null;
  motivoNaoRealizacao: string | null;
  observacoes: string | null;
};
