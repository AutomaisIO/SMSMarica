/** Um login que já apareceu como "Op. autorizador" no export da agenda do SISREG. */
export type OperadorSisreg = {
  login: string;
  autorizacoes: number;
  primeiraAutorizacao: string | null;
  ultimaAutorizacao: string | null;
  habilitado: boolean;
  usuarioId: string | null;
  usuarioNome: string | null;
};

export type OperadoresConfiguracao = {
  operadores: OperadorSisreg[];
  habilitados: number;
};

/** Os números de um período (atual ou anterior, para comparar). */
export type ResumoPeriodo = {
  de: string;
  ate: string;
  autorizacoes: number;
  valorRegulado: number;
  operadores: number;
  diasComAtividade: number;
  mediaDiaCorrido: number;
  mediaDiaComAtividade: number;
  /** Nulo no período anterior. */
  esperaMedianaDias: number | null;
  antecedenciaMedianaDias: number | null;
  percentualFimDeSemana: number;
};

export type SerieDia = { dia: string; autorizacoes: number; operadores: number; valorRegulado: number };
export type SerieMes = { mes: string; autorizacoes: number; operadores: number; valorRegulado: number };

export type DiaSemana = {
  /** 1 = segunda … 7 = domingo. */
  isoDia: number;
  rotulo: string;
  autorizacoes: number;
  diasComAtividade: number;
  mediaPorDiaComAtividade: number;
};

export type TopItem = { rotulo: string; autorizacoes: number; valorRegulado: number; operadores: number };

/** Uma pessoa (ou login sem pessoa associada) no período. */
export type OperadorPeriodo = {
  /** `u:<id do usuário>` ou `l:<LOGIN>`. */
  chave: string;
  nome: string;
  usuarioId: string | null;
  logins: string[];
  autorizacoes: number;
  valorRegulado: number;
  diasTrabalhados: number;
  diasCorridos: number;
  mediaDiaCorrido: number;
  mediaDiaTrabalhado: number;
  picoDiario: number;
  diaDoPico: string | null;
  percentualFimDeSemana: number;
  esperaMedianaDias: number | null;
  esperaP90Dias: number | null;
  antecedenciaMedianaDias: number | null;
  agendaNovaMedianaDias: number | null;
  agendaNovaAutorizacoes: number;
  unidadesExecutantes: number;
  unidadesSolicitantes: number;
  procedimentos: number;
  percentualProprioPedido: number;
};

export type EquipeEstatistica = {
  diasCorridos: number;
  habilitados: number;
  atual: ResumoPeriodo;
  anterior: ResumoPeriodo;
  autorizacoesTodosOsLogins: number;
  mediaOperadoresPorDiaUtil: number;
  concentracaoTop3Percentual: number;
  pico: SerieDia | null;
  porDia: SerieDia[];
  porMes: SerieMes[];
  porDiaSemana: DiaSemana[];
  ranking: OperadorPeriodo[];
  topProcedimentos: TopItem[];
  topUnidadesExecutantes: TopItem[];
  topUnidadesSolicitantes: TopItem[];
};

export type IndividualEstatistica = {
  chave: string;
  nome: string;
  usuarioId: string | null;
  logins: string[];
  diasCorridos: number;
  metricas: OperadorPeriodo | null;
  atual: ResumoPeriodo;
  anterior: ResumoPeriodo;
  porDia: SerieDia[];
  porMes: SerieMes[];
  porDiaSemana: DiaSemana[];
  topProcedimentos: TopItem[];
  topUnidadesExecutantes: TopItem[];
  topUnidadesSolicitantes: TopItem[];
};

export type Periodo = { de: string; ate: string };
