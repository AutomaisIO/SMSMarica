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
  /** Nascidos mortos (ID_CONDICAO_NASCIMENTO='M') — 100% preenchido. */
  natimortos: number;
  comMalformacao: number;
  malformacaoSemInfo: number;
  /**
   * Idade gestacional pela codificação do SINASC: a termo (37–41 semanas),
   * prematuro tardio (32–36) e pós-termo (42+). Decodificação conferida contra o
   * peso médio e o campo de prematuridade.
   */
  aTermo: number;
  prematuroTardio: number;
  posTermo: number;
  gestacaoSemInfo: number;
  gravidezUnica: number;
  gravidezMultipla: number;
  apgar1Abaixo7: number;
  estaturaMedia: number | null;
  perimetroCefalicoMedio: number | null;
  /** Idade da mãe pela FIA do parto — ligação de 100% dos nascimentos. */
  idadeMediaMae: number | null;
  maeAte17: number;
  maeMenor20: number;
  mae35Mais: number;
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

/** Os quatro períodos de todo seletor do painel. */
export type PeriodoPainel = 'hoje' | 'ontem' | 'mesAtual' | 'mesAnterior';

export interface EsperaPorCorPeriodos {
  hoje: EsperaCor[];
  ontem: EsperaCor[];
  mesAtual: EsperaCor[];
  mesAnterior: EsperaCor[];
}

export interface EsperaPorCor {
  atualizadoEm: string;
  periodos: EsperaPorCorPeriodos;
}

/**
 * Ocupação do momento. O numerador são PACIENTES reais, não o flag do leito —
 * assim casa com "internados agora" no resto do painel.
 *
 * `leitos` é a CAPACIDADE OPERACIONAL: leito de internação, ativo, não bloqueado.
 * Leito extra, virtual e desativado não são capacidade e vêm contados à parte.
 *
 * `excedente` (estouro de cota) NÃO é o mesmo que `emLeitoExtra` (deitado em cama
 * rotulada extra). O rótulo é do cadastro da cama; o NIR aloca em extra por motivo
 * clínico mesmo com ordinário livre. Só `excedente` mede lotação.
 */
export interface Ocupacao {
  /** Capacidade operacional — o denominador da taxa. */
  leitos: number;
  /** Internados agora, inclusive quem excede a capacidade. */
  ocupados: number;
  /** Capacidade ainda disponível, somada por setor. Invariante: `ocupados = leitos - livres + excedente`. */
  livres: number;
  /** Leito de internação ativo, porém fechado/interditado. */
  bloqueados: number;
  /** Internados além da capacidade do próprio setor — a lotação de verdade. */
  excedente: number;
  /** Deitados em cama rotulada extra/virtual/desativada. Diagnóstico de cadastro, não lotação. */
  emLeitoExtra: number;
  extras: number;
  virtuais: number;
  desativados: number;
  /** 0–N sobre a capacidade; passa de 100 quando o setor estoura a cota. */
  taxa: number | null;
}

export interface SetorOcupacao {
  setor: string;
  leitos: number;
  ocupados: number;
  bloqueados: number;
  /** Derivado no back: `max(0, leitos - ocupados)`. */
  livres: number;
  /** Derivado no back: `max(0, ocupados - leitos)`. Exclusivo com `livres`. */
  excedente: number;
  emLeitoExtra: number;
  extras: number;
  virtuais: number;
  desativados: number;
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
  diagnosticos?: Diagnosticos | null;
}

export interface CidRanking {
  codigo: string;
  descricao: string;
  qtd: number;
  /** Participação do CID dentro da cor (0–100). */
  pct: number | null;
}

export interface DiagnosticosDaCor {
  cor: CorTriagem;
  /** Boletins da cor COM CID registrado — é o denominador do `pct`. */
  boletins: number;
  cids: CidRanking[];
}

export interface DiagnosticosPeriodos {
  hoje: DiagnosticosDaCor[];
  ontem: DiagnosticosDaCor[];
  mesAtual: DiagnosticosDaCor[];
  mesAnterior: DiagnosticosDaCor[];
}

/** Só o Conde tem: nas UPAs o CID da classificação não é preenchido. */
export interface Diagnosticos {
  atualizadoEm: string;
  periodos: DiagnosticosPeriodos;
  escopo: string | null;
}

/** As visões do painel — o seletor de cima. */
export type VisaoPainel = 'emergencia' | 'leitos' | 'maternidade';

export interface Painel {
  geradoEm: string;
  /** Consolidado: só está ok com TODAS as bases ok. */
  status: StatusFonte;
  fontes: FonteInfo[];
  unidades: UnidadePainel[];
}
