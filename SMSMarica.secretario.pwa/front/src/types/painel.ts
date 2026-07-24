/**
 * Types do GET /api/painel — gerados a partir de docs/contrato-painel.json.
 * O contrato é a verdade: não acrescentar campos que o back não devolve.
 */

export type CorTriagem = 'VERMELHO' | 'AMARELO' | 'VERDE' | 'AZUL' | 'SEM_CLASSIFICACAO';

export interface StatusOracle {
  ok: boolean;
  ultimoErro: string | null;
  ultimaAtualizacaoOk: string | null;
}

export interface AguardandoPorCor {
  cor: CorTriagem;
  qtd: number;
  /** Minutos médios desde a chegada (null quando não há ninguém na fila). */
  minMedioEspera: number | null;
}

export interface Agora {
  atualizadoEm: string;
  aguardandoMedico: number;
  aguardandoPorCor: AguardandoPorCor[];
  emAtendimento: number;
  internadosAgora: number;
  /**
   * Split por UNIDADE (leito atual), não por FIA.ID_INTERNACAO: no HMCML o 'E'
   * daquele campo não é "eletiva" — 98% dos casos estão na maternidade e o caráter
   * oficial do SUS é urgência em 100% deles. Ver docs/consultas-oracle.md §Q5.
   */
  internadosMaternidade: number;
  internadosDemais: number;
  /** Média de dias dos internados atuais (null quando não calculável — ex.: cold start). */
  mediaDiasInternacao: number | null;
  atendimentosHoje: number;
  internacoesHoje: number;
}

export interface PontoDia {
  /** Data ISO (yyyy-mm-dd). */
  dia: string;
  qtd: number;
  /** Split maternidade/demais — presente na serieDiaria de internações. */
  maternidade?: number;
  demais?: number;
}

export interface PontoHora {
  hora: number;
  qtd: number;
}

export interface MesAtendimentoAnterior {
  rotulo: string;
  total: number;
  dias: number;
  mediaDiaria: number;
}

export interface MesAtendimentoAtual {
  rotulo: string;
  total: number;
  diasCompletos: number;
  totalDiasCompletos: number;
  mediaDiaria: number;
}

export interface Atendimentos {
  atualizadoEm: string;
  mesAnterior: MesAtendimentoAnterior;
  mesAtual: MesAtendimentoAtual;
  hoje: { total: number };
  serieDiaria: PontoDia[];
  porHoraHoje: PontoHora[];
}

export interface MesInternacao {
  rotulo: string;
  total: number;
  maternidade: number;
  demais: number;
  mediaDiaria: number;
}

export interface Internacoes {
  atualizadoEm: string;
  mesAnterior: MesInternacao;
  mesAtual: MesInternacao;
  hoje: { total: number; maternidade: number; demais: number };
  serieDiaria: PontoDia[];
}

/** Um período do livro de partos (INFOSAUDE.NASCIMENTO). */
export interface MaternidadePeriodo {
  rotulo: string;
  partos: number;
  cesareas: number;
  vaginais: number;
  prematuros: number;
  /** Nascidos com menos de 2,5 kg (o peso vem em QUILOS na base). */
  baixoPeso: number;
  pesoMedioKg: number | null;
  apgar5Abaixo7: number;
  meninas: number;
  meninos: number;
  /** null no período "hoje" (não faz média de um dia só). */
  mediaDiaria: number | null;
  /** 0–100; null quando não houve parto no período. */
  pctCesarea: number | null;
}

export interface PontoDiaPartos {
  dia: string;
  qtd: number;
  cesareas?: number;
}

export interface Maternidade {
  atualizadoEm: string;
  mesAnterior: MaternidadePeriodo;
  mesAtual: MaternidadePeriodo;
  hoje: MaternidadePeriodo;
  serieDiaria: PontoDiaPartos[];
}

export interface EsperaCor {
  cor: CorTriagem;
  pacientes: number;
  comAtendimento: number;
  mediaAteTriagem: number | null;
  mediaEspera: number | null;
  medianaEspera: number | null;
  p90Espera: number | null;
  metaMin: number | null;
  pctNaMeta: number | null;
}

export type PeriodoEspera = 'hoje' | 'mesAtual' | 'mesAnterior';

export interface EsperaPorCorPeriodos {
  hoje: EsperaCor[];
  mesAtual: EsperaCor[];
  mesAnterior: EsperaCor[];
}

export interface EsperaPorCor {
  atualizadoEm: string;
  periodos: EsperaPorCorPeriodos;
}

/**
 * No cold start o back responde 200 com apenas `agora` preenchido — as demais
 * seções chegam null/ausentes até o primeiro ciclo dos consolidados. A UI
 * renderiza cada seção de forma independente (seção ausente → skeleton).
 */
export interface Painel {
  geradoEm: string;
  fonte: string;
  oracle: StatusOracle;
  agora?: Agora | null;
  atendimentos?: Atendimentos | null;
  internacoes?: Internacoes | null;
  esperaPorCor?: EsperaPorCor | null;
  maternidade?: Maternidade | null;
}
