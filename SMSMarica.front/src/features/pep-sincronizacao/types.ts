export type ModoSincronizacao = 'Completo' | 'Incremental';
export type EscopoSincronizacao = 'Limitado' | 'Tudo';

/** Uma base de PEP candidata à importação (vem do cadastro de fontes da IA). */
export type BasePep = {
  id: string;
  nome: string;
  tipo: string; // Salux, Mv, Eco...
  ambiente: string; // Producao, Treinamento
  suportada: boolean;
  ultimaSincronizacaoEm: string | null;
  /** Cursor de retomada salvo (cd_paciente) de uma importação completa interrompida. */
  cursorPacienteCd: number | null;
};

export type IniciarImportacaoPayload = {
  fonteId: string;
  modo: ModoSincronizacao;
  escopo: EscopoSincronizacao;
  maxMedicos: number | null;
  maxPacientes: number | null;
  apagarAntes: boolean;
  concorrencia: number | null;
  /** Ponteiro inicial de cd_paciente (Completo + Tudo). Null = começa do topo. */
  cursorPacienteInicial: number | null;
};

export type ContadoresImportacao = {
  medicos: number;
  pacientes: number;
  encounters: number;
  conditions: number;
  medicationRequests: number;
  documentReferences: number;
  observations: number;
  falhas: number;
  /** Reenvios por saturação transitória (farol de backpressure). Só conta no run vivo. */
  retentativas: number;
};

export type StatusImportacao = {
  execucaoId: string | null;
  emExecucao: boolean;
  fonteId: string | null;
  fonteNome: string | null;
  modo: string | null;
  escopo: string | null;
  status: string; // Pendente, EmExecucao, Concluido, Erro, Nenhuma
  faseAtual: string | null;
  iniciadoEm: string | null;
  finalizadoEm: string | null;
  decorridoSegundos: number | null;
  contadores: ContadoresImportacao;
  mensagemErro: string | null;
  ultimasFalhas: string[];
};

export type ExecucaoImportacao = {
  id: string;
  fonteId: string;
  fonteNome: string;
  modo: string;
  escopo: string;
  status: string;
  iniciadoEm: string;
  finalizadoEm: string | null;
  duracaoSegundos: number | null;
  contadores: ContadoresImportacao;
  temposJson: string | null;
  mensagemErro: string | null;
  /** Origem do disparo: Manual (operador) ou Agendado (motor contínuo). */
  disparo: string;
};

/** Agenda do sincronismo contínuo de uma base (ADR-0024). */
export type AgendaPep = {
  fonteId: string;
  fonteNome: string;
  ativo: boolean;
  intervaloMinutos: number;
  janelaInicioLocal: string | null; // "HH:mm:ss" em hora de Brasília
  janelaFimLocal: string | null;
  medicoRescanHoras: number;
  falhasConsecutivas: number;
  proximoRunEm: string | null;
  pausadoAte: string | null;
  atualizadoEm: string;
};

export type SalvarAgendaPayload = {
  fonteId: string;
  ativo: boolean;
  intervaloMinutos: number;
  janelaInicioLocal?: string | null;
  janelaFimLocal?: string | null;
  medicoRescanHoras?: number | null;
};

/** Diagnóstico origem×hub: marcas d'água, pendências na origem e contagens do hub. */
export type DiagnosticoPep = {
  fonteId: string;
  fonteNome: string;
  slug: string;
  ultimoSyncMedicoEm: string | null;
  ultimoSyncPacienteEm: string | null;
  ultimoSyncBaaEm: string | null;
  ultimoSyncEdocEm: string | null;
  ultimoSyncFiaEm: string | null;
  ultimoSyncEdocLogId: number | null;
  pacientesPendentes: number | null;
  baasPendentes: number | null;
  fiasPendentes: number | null;
  edocLogPendentes: number | null;
  hub: Record<string, number | string | null>;
};

/** Veredicto da consulta oficial de CPF sobre uma divergência de identidade. */
export type VeredictoDivergencia =
  | 'Indefinido'
  | 'OrigemCorreta'
  | 'HubCorreto'
  | 'AmbosNegados'
  | 'Inconclusivo';

export type StatusDivergencia = 'Pendente' | 'Verificada' | 'NaoConclusiva' | 'Ignorada';

/**
 * Divergência de identidade origem×hub para o MESMO CPF (hoje: data de nascimento).
 * Enquanto não há veredicto — ou quando ele aponta contra a origem — o campo fica
 * congelado no hub (a importação não sobrescreve).
 */
export type DivergenciaIdentidade = {
  id: string;
  fonteId: string;
  fonteSlug: string;
  cdPaciente: number;
  cpf: string;
  tipo: string;
  valorOrigem: string;
  valorHub: string;
  nomeOrigem: string | null;
  nomeHub: string | null;
  patientIdHub: string | null;
  status: StatusDivergencia;
  veredicto: VeredictoDivergencia;
  veredictoMotor: string | null;
  valorCorreto: string | null;
  nomeOficial: string | null;
  detalhe: string | null;
  ocorrencias: number;
  criadoEm: string;
  atualizadoEm: string;
  verificadoEm: string | null;
  resolvidoEm: string | null;
};

export type ResumoDivergencias = {
  total: number;
  pendentes: number;
  naoConclusivas: number;
  ignoradas: number;
  origemCorreta: number;
  hubCorreto: number;
  ambosNegados: number;
  /** CPFs com o campo congelado agora (a origem não sobrescreve o hub). */
  congelados: number;
};

export type ResultadoVerificacaoDivergencias = {
  analisadas: number;
  origemCorreta: number;
  hubCorreto: number;
  ambosNegados: number;
  naoConclusivas: number;
  interrompidaPorIndisponibilidade: boolean;
};
