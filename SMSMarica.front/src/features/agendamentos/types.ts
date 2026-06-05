export type DiaSemana =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

export type StatusAgendamento = 'Agendado' | 'Confirmado' | 'Realizado' | 'Cancelado' | 'Faltou';

export type FinalidadeAgenda = 'Consulta' | 'Exame';

/** Tipo de agenda na UI: pool da especialidade, médico específico (retorno) ou equipamento (exame). */
export type TipoAgenda = 'ConsultaEspecialidade' | 'ConsultaMedico' | 'Exame';

export const TIPOS_AGENDA: { id: TipoAgenda; rotulo: string; descricao: string }[] = [
  { id: 'ConsultaEspecialidade', rotulo: 'Consulta — especialidade (pool)', descricao: 'Qualquer médico da especialidade' },
  { id: 'ConsultaMedico', rotulo: 'Consulta — médico específico', descricao: 'Para retorno/revisão (mesmo médico)' },
  { id: 'Exame', rotulo: 'Exame — equipamento', descricao: 'Agenda de um equipamento de imagem' },
];

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
  finalidade: FinalidadeAgenda;
  unidadeNome: string;
  alvo: string;
  duracaoSlotMinutos: number;
  ativo: boolean;
};

export type Agenda = {
  id: string;
  finalidade: FinalidadeAgenda;
  unidadeId: string;
  unidadeNome: string;
  especialidadeId: string | null;
  especialidadeNome: string | null;
  medicoId: string | null;
  medicoNome: string | null;
  medicoCns: string | null;
  equipamentoId: string | null;
  equipamentoNome: string | null;
  alvo: string;
  duracaoSlotMinutos: number;
  vigenciaInicio: string;
  vigenciaFim: string | null;
  ativo: boolean;
  recorrencias: DisponibilidadeRecorrente[];
  criadoEm: string;
};

export type CadastrarAgendaPayload = {
  finalidade: FinalidadeAgenda;
  unidadeId: string;
  especialidadeId?: string | null;
  medicoId?: string | null;
  equipamentoId?: string | null;
  duracaoSlotMinutos: number;
  vigenciaInicio: string;
  vigenciaFim?: string | null;
};

export type AtualizarAgendaPayload = {
  duracaoSlotMinutos: number;
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
  tipoExameId: string | null;
  observacao: string | null;
  criadoEm: string;
};

export type AgendarPayload = {
  agendaId: string;
  pacienteId: string;
  inicioEm: string;
  tipoExameId?: string | null;
  observacao?: string | null;
};
