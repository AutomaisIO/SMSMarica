export type DiaSemana =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

export type StatusAgendamento = 'Agendado' | 'Confirmado' | 'Realizado' | 'Cancelado' | 'Faltou';

export const DIAS_SEMANA: { id: DiaSemana; rotulo: string }[] = [
  { id: 'Monday', rotulo: 'Segunda' },
  { id: 'Tuesday', rotulo: 'Terça' },
  { id: 'Wednesday', rotulo: 'Quarta' },
  { id: 'Thursday', rotulo: 'Quinta' },
  { id: 'Friday', rotulo: 'Sexta' },
  { id: 'Saturday', rotulo: 'Sábado' },
  { id: 'Sunday', rotulo: 'Domingo' },
];

export const STATUS_LABEL: Record<StatusAgendamento, string> = {
  Agendado: 'Agendado',
  Confirmado: 'Confirmado',
  Realizado: 'Realizado',
  Cancelado: 'Cancelado',
  Faltou: 'Faltou',
};

export type DisponibilidadeRecorrente = {
  id: string;
  diaSemana: DiaSemana;
  horaInicio: string;
  horaFim: string;
  vigenciaInicio: string | null;
  vigenciaFim: string | null;
  ativo: boolean;
};

export type AgendaListItem = {
  id: string;
  unidadeNome: string;
  especialidadeNome: string;
  medicoId: string;
  medicoNome: string;
  duracaoConsultaMinutos: number;
  ativo: boolean;
};

export type Agenda = {
  id: string;
  unidadeId: string;
  unidadeNome: string;
  especialidadeId: string;
  especialidadeNome: string;
  medicoId: string;
  medicoNome: string;
  medicoCns: string | null;
  duracaoConsultaMinutos: number;
  vigenciaInicio: string;
  vigenciaFim: string | null;
  ativo: boolean;
  recorrencias: DisponibilidadeRecorrente[];
  criadoEm: string;
};

export type CadastrarAgendaPayload = {
  unidadeId: string;
  especialidadeId: string;
  medicoId: string;
  duracaoConsultaMinutos: number;
  vigenciaInicio: string;
  vigenciaFim?: string | null;
};

export type AtualizarAgendaPayload = {
  duracaoConsultaMinutos: number;
  vigenciaInicio: string;
  vigenciaFim?: string | null;
  ativo: boolean;
};

export type AdicionarRecorrenciaPayload = {
  diaSemana: DiaSemana;
  horaInicio: string;
  horaFim: string;
  vigenciaInicio?: string | null;
  vigenciaFim?: string | null;
};

export type DisponibilidadeAvulsa = {
  id: string;
  inicioEm: string;
  fimEm: string;
  motivo: string | null;
};

export type AdicionarAvulsoPayload = { inicioEm: string; fimEm: string; motivo?: string | null };

export type BloqueioAgenda = {
  id: string;
  inicioEm: string;
  fimEm: string;
  motivo: string;
};

export type AdicionarBloqueioPayload = { inicioEm: string; fimEm: string; motivo: string };

export type DisponibilidadesAgenda = {
  avulsos: DisponibilidadeAvulsa[];
  bloqueios: BloqueioAgenda[];
};

export type SlotLivre = { inicioEm: string; fimEm: string };

export type AgendamentoListItem = {
  id: string;
  pacienteId: string;
  pacienteNome: string;
  inicioEm: string;
  fimEm: string;
  status: StatusAgendamento;
};

export type Agendamento = {
  id: string;
  agendaId: string;
  pacienteId: string;
  pacienteNome: string;
  pacienteCns: string | null;
  inicioEm: string;
  fimEm: string;
  status: StatusAgendamento;
  observacao: string | null;
  criadoEm: string;
};

export type AgendarPayload = {
  agendaId: string;
  pacienteId: string;
  inicioEm: string;
  observacao?: string | null;
};
