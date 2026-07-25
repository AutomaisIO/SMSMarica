/**
 * Types do GET /api/painel — gerados a partir de docs/contrato-painel.json.
 * O contrato é a verdade: não acrescentar campos que o back não devolve.
 *
 * Desde 25/07/2026 o painel é MULTI-UNIDADE: o snapshot traz `unidades[]` com
 * geral / conde / upa, cada uma com as mesmas seções. O que não existe na unidade
 * vem NULO, nunca zerado — a UPA não interna nem tem maternidade.
 */

export type CorTriagem =
  | 'VERMELHO'
  | 'LARANJA'
  | 'AMARELO'
  | 'VERDE'
  | 'AZUL'
  | 'SEM_CLASSIFICACAO';

export type UnidadeId = 'geral' | 'conde' | 'upa' | 'santarita';

export interface StatusFonte {
  ok: boolean;
  ultimoErro: string | null;
  ultimaAtualizacaoOk: string | null;
}

/** Procedência de uma base, com o estado da última leitura. */
export interface FonteInfo {
  /** Casa com o id da unidade real correspondente ('conde' | 'upa'). */
  id: string;
  nome: string;
  status: StatusFonte;
}

export interface AguardandoPorCor {
  cor: CorTriagem;
  qtd: number;
  /** Minutos médios desde a chegada (null quando não há ninguém na fila). */
  minMedioEspera: number | null;
}

/**
 * Internados neste momento, nas três faixas exclusivas. Nulo na UPA — a unidade
 * não interna (a tabela Internacao do HIS dela parou em 25/01/2026).
 */
export interface InternadosAgora {
  total: number;
  /**
   * Split por UNIDADE (leito atual), não por FIA.ID_INTERNACAO: no HMCML o 'E'
   * daquele campo não é "eletiva" — 98% dos casos estão na maternidade e o caráter
   * oficial do SUS é urgência em 100% deles. Ver docs/consultas-oracle.md §Q5.
   */
  maternidade: number;
  /** Crianças e adolescentes (≤17 na entrada) internados FORA da maternidade. */
  ate17: number;
  adultos: number;
  /** Média de dias dos internados atuais (null quando não calculável — ex.: cold start). */
  mediaDiasInternacao: number | null;
  internacoesHoje: number;
  /** Na aba "geral": diz que o número é só do Conde. */
  escopo: string | null;
}

export interface Agora {
  atualizadoEm: string;
  aguardandoMedico: number;
  aguardandoPorCor: AguardandoPorCor[];
  emAtendimento: number;
  atendimentosHoje: number;
  internados: InternadosAgora | null;
}

export interface PontoDia {
  /** Data ISO (yyyy-mm-dd). */
  dia: string;
  qtd: number;
  /**
   * Faixas exclusivas — presentes na serieDiaria de internações. A ordem de
   * precedência é maternidade → até 17 anos → adultos: sem isso os recém-nascidos
   * (que estão no berçário) dominariam a faixa infantil.
   */
  maternidade?: number;
  ate17?: number;
  adultos?: number;
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
  ate17: number;
  adultos: number;
  mediaDiaria: number;
}

export interface Internacoes {
  atualizadoEm: string;
  mesAnterior: MesInternacao;
  mesAtual: MesInternacao;
  hoje: { total: number; maternidade: number; ate17: number; adultos: number };
  serieDiaria: PontoDia[];
  /** Na aba "geral": a unidade a que o número pertence de fato. */
  escopo: string | null;
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
  /** Na aba "geral": a única maternidade da rede é a do Conde. */
  escopo: string | null;
}

export interface EsperaCor {
  cor: CorTriagem;
  pacientes: number;
  comAtendimento: number;
  mediaAteTriagem: number | null;
  mediaEspera: number | null;
  medianaEspera: number | null;
  p90Espera: number | null;
  /**
   * Meta em minutos, do cadastro da unidade. Na aba "geral" `metaMin` e `pctNaMeta`
   * vêm SEMPRE nulos: cada unidade tem sua própria régua para a mesma cor (Amarelo
   * é 30 min no Conde, 60 na UPA Maricá e 30 em Santa Rita), então a rede não tem
   * meta e o consolidado mostra só volume e tempo.
   */
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
 * Ocupação do momento. O numerador são PACIENTES reais, não o flag do leito —
 * assim casa com "internados agora" no resto do painel. `leitos` inclui os
 * bloqueados; a `taxa` não, porque leito interditado não é capacidade.
 */
export interface Ocupacao {
  leitos: number;
  ocupados: number;
  livres: number;
  bloqueados: number;
  /** 0–100 sobre os leitos disponíveis; null quando não há leito disponível. */
  taxa: number | null;
}

export interface SetorOcupacao {
  setor: string;
  leitos: number;
  ocupados: number;
  bloqueados: number;
  taxa: number | null;
}

export interface PerfilInternados {
  total: number;
  homens: number;
  mulheres: number;
  semSexo: number;
  ate17: number;
  adultos: number;
  idosos: number;
  idadeMedia: number | null;
  /** Dias já decorridos de quem está internado agora (≠ permanência das altas). */
  diasMedios: number | null;
}

export interface PermanenciaSegmento {
  segmento: string;
  altas: number;
  mediaDias: number | null;
}

/** Permanência das ALTAS do período — o indicador clássico. */
export interface Permanencia {
  rotulo: string;
  altas: number;
  mediaDias: number | null;
  medianaDias: number | null;
  p90Dias: number | null;
  segmentos: PermanenciaSegmento[];
}

/** Fluxo de encaminhamento à observação (só nas UPAs). */
export interface ObservacaoFluxo {
  rotulo: string;
  encaminhados: number;
  classificados: number;
}

export interface Leitos {
  atualizadoEm: string;
  /** Null quando o cadastro da unidade não sustenta o número — ver `indisponivel`. */
  ocupacao: Ocupacao | null;
  setores: SetorOcupacao[];
  /** Só onde há internação de verdade (o Conde). */
  perfil: PerfilInternados | null;
  permanencia: Permanencia | null;
  observacao: ObservacaoFluxo | null;
  escopo: string | null;
  /** Motivo da ausência de ocupação, para a tela dizer o que falta em vez de zero. */
  indisponivel: string | null;
}

/**
 * Uma aba do painel. No cold start as seções chegam nulas e a UI renderiza
 * skeleton por seção — seção ausente nunca vira erro global.
 */
export interface UnidadePainel {
  id: UnidadeId;
  /** Rótulo curto do seletor. */
  rotulo: string;
  /** Nome por extenso. */
  nome: string;
  /** Linha de procedência dos números. */
  fonte: string;
  /** Cores que ESTA unidade usa — as demais são apagadas em vez de mostradas como 0. */
  coresUsadas: CorTriagem[];
  agora?: Agora | null;
  atendimentos?: Atendimentos | null;
  internacoes?: Internacoes | null;
  esperaPorCor?: EsperaPorCor | null;
  maternidade?: Maternidade | null;
  leitos?: Leitos | null;
}

/** As duas visões do painel — o seletor de cima. */
export type VisaoPainel = 'emergencia' | 'leitos';

export interface Painel {
  geradoEm: string;
  /** Consolidado: só está ok com TODAS as bases ok. */
  status: StatusFonte;
  fontes: FonteInfo[];
  unidades: UnidadePainel[];
}
