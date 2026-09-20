export type { Periodo } from '@/shared/lib/estatisticasPeriodo';

/** Qual fila espelhada a tela lê. Vai para a URL da API e para os textos. */
export type FonteExterna = 'ser' | 'sernit';

/** Um nome que já apareceu como "Usuário" na trilha de eventos do SER/SERNIT. */
export type OperadorExterno = {
  nome: string;
  acoes: number;
  primeiraAcao: string | null;
  ultimaAcao: string | null;
  lotacaoMaisComum: string | null;
  habilitado: boolean;
};

export type OperadoresExternosConfiguracao = {
  operadores: OperadorExterno[];
  habilitados: number;
};

/** Os números de um período (atual ou anterior, para comparar). */
export type ResumoPeriodoExterno = {
  de: string;
  ate: string;
  acoes: number;
  agendamentos: number;
  cancelamentos: number;
  followUps: number;
  pendencias: number;
  solicitacoes: number;
  operadores: number;
  diasComAtividade: number;
  mediaDiaCorrido: number;
  mediaDiaComAtividade: number;
  /** Nulo no período anterior. */
  esperaMedianaDias: number | null;
  percentualFimDeSemana: number;
  percentualForaDoExpediente: number;
};

export type SerieDiaExterno = { dia: string; acoes: number; operadores: number; agendamentos: number };
export type SerieMesExterno = { mes: string; acoes: number; operadores: number; agendamentos: number };

export type DiaSemanaExterno = {
  /** 1 = segunda … 7 = domingo. */
  isoDia: number;
  rotulo: string;
  acoes: number;
  diasComAtividade: number;
  mediaPorDiaComAtividade: number;
};

/** 0..23 no relógio de Brasília — sempre as 24 horas. */
export type HoraExterno = { hora: number; acoes: number; agendamentos: number };

export type TopItemExterno = { rotulo: string; acoes: number; operadores: number };

/** Uma pessoa (nome como o sistema externo grava) no período. */
export type OperadorExternoPeriodo = {
  /** `o:<NOME>`. */
  chave: string;
  nome: string;
  acoes: number;
  agendamentos: number;
  cancelamentos: number;
  followUps: number;
  pendencias: number;
  solicitacoes: number;
  diasTrabalhados: number;
  diasCorridos: number;
  mediaDiaCorrido: number;
  mediaDiaTrabalhado: number;
  picoDiario: number;
  diaDoPico: string | null;
  percentualFimDeSemana: number;
  esperaMedianaDias: number | null;
  esperaP90Dias: number | null;
  /** Hora decimal (8,5 = 8h30). */
  horaInicioMediana: number | null;
  horaFimMediana: number | null;
  recursos: number;
  unidadesExecutoras: number;
  lotacoes: string[];
};

export type EquipeExternaEstatistica = {
  fonte: string;
  diasCorridos: number;
  habilitados: number;
  atual: ResumoPeriodoExterno;
  anterior: ResumoPeriodoExterno;
  acoesTodosOsOperadores: number;
  mediaOperadoresPorDiaUtil: number;
  concentracaoTop3Percentual: number;
  pico: SerieDiaExterno | null;
  porDia: SerieDiaExterno[];
  porMes: SerieMesExterno[];
  porDiaSemana: DiaSemanaExterno[];
  porHora: HoraExterno[];
  ranking: OperadorExternoPeriodo[];
  porTipoEvento: TopItemExterno[];
  topRecursos: TopItemExterno[];
  topUnidadesExecutoras: TopItemExterno[];
  topLotacoes: TopItemExterno[];
};

export type IndividualExternoEstatistica = {
  fonte: string;
  chave: string;
  nome: string;
  diasCorridos: number;
  metricas: OperadorExternoPeriodo | null;
  atual: ResumoPeriodoExterno;
  anterior: ResumoPeriodoExterno;
  porDia: SerieDiaExterno[];
  porMes: SerieMesExterno[];
  porDiaSemana: DiaSemanaExterno[];
  porHora: HoraExterno[];
  porTipoEvento: TopItemExterno[];
  topRecursos: TopItemExterno[];
  topUnidadesExecutoras: TopItemExterno[];
  topLotacoes: TopItemExterno[];
};
