/**
 * Indicadores contratuais do HMCML. O "motor" de cada indicador é o SQL guardado no cadastro
 * e executado read-only contra a base de origem (Oracle do Salux) — ver ADR-0022.
 *
 * O cálculo espelha as fórmulas da planilha contratual: resultado = numerador / denominador
 * (com multiplicador só na densidade) e a pontuação é tudo-ou-nada contra a meta.
 */

export type AbaIndicador =
  | 'Adulto'
  | 'Pediatrico'
  | 'MaternoInfantil'
  | 'PerfilEpidemiologico'
  | 'Institucional';

export type TipoResultadoIndicador =
  | 'Razao'
  | 'Densidade'
  | 'Absoluto'
  | 'Distribuicao'
  | 'Agrupador'
  | 'Media';

export type MetaOperador =
  | 'MenorOuIgual'
  | 'MaiorOuIgual'
  | 'Menor'
  | 'Maior'
  | 'Igual'
  | 'Entre';

export type SituacaoIndicador = 'Validado' | 'NaoValidado' | 'SemMotor' | 'ForaDoBanco';

export type LinhaDistribuicao = {
  rotulo: string;
  quantidade: number;
};

export type ResultadoIndicador = {
  valor: number | null;
  numerador: number | null;
  denominador: number | null;
  distribuicao: LinhaDistribuicao[] | null;
  atingiuMeta: boolean | null;
  pontuacaoApurada: number | null;
  duracaoMs: number;
  executadoEm: string;
  erro: string | null;
};

export type IndicadorResumo = {
  id: string;
  aba: AbaIndicador;
  numero: string;
  ordem: number;
  indicadorPaiId: string | null;
  nome: string;
  meta: string | null;
  metaOperador: MetaOperador | null;
  metaValor: number | null;
  metaValorMaximo: number | null;
  pontuacao: number | null;
  tipoResultado: TipoResultadoIndicador;
  unidadeMedida: string | null;
  fatorDensidade: number | null;
  situacao: SituacaoIndicador;
  temMotor: boolean;
  ressalva: string | null;
  ativo: boolean;
  resultado: ResultadoIndicador | null;
  /**
   * Campos de rastreabilidade — vêm na listagem para a exportação em Excel ser um documento
   * auditável e autossuficiente (quem recebe a planilha vê de onde o número saiu sem precisar
   * de acesso ao sistema).
   */
  memoriaCalculo: string | null;
  fonteDeclarada: string | null;
  fonteNome: string | null;
  sql: string | null;
  temAnalitico: boolean;
};

/**
 * Evidência linha a linha por trás de um indicador: os registros que entraram na conta e os que
 * foram excluídos, com o motivo. Retorno cru do SQL analítico — colunas do jeito que a consulta
 * as nomeou. Nada é persistido: é sempre uma leitura nova da base de origem.
 */
export type AnaliticoIndicador = {
  indicadorId: string;
  numero: string;
  nome: string;
  colunas: string[];
  linhas: (string | number | boolean | null)[][];
  /** Posição da coluna `incluido` em `colunas`; -1 quando o SQL não a trouxe. */
  indiceIncluido: number;
  indiceMotivo: number;
  incluidos: number;
  excluidos: number;
  /** Bateu no teto de linhas — a evidência está incompleta e a planilha precisa dizer isso. */
  truncado: boolean;
  limiteLinhas: number;
  duracaoMs: number;
  executadoEm: string;
  erro: string | null;
};

export type IndicadorDetalhe = {
  id: string;
  aba: AbaIndicador;
  numero: string;
  ordem: number;
  indicadorPaiId: string | null;
  nome: string;
  memoriaCalculo: string | null;
  fonteDeclarada: string | null;
  meta: string | null;
  metaOperador: MetaOperador | null;
  metaValor: number | null;
  metaValorMaximo: number | null;
  pontuacao: number | null;
  tipoResultado: TipoResultadoIndicador;
  unidadeMedida: string | null;
  fatorDensidade: number | null;
  situacao: SituacaoIndicador;
  fonteId: string | null;
  fonteNome: string | null;
  sql: string | null;
  sqlAnalitico: string | null;
  ressalva: string | null;
  ativo: boolean;
  totalVersoes: number;
};

export type IndicadorVersao = {
  id: string;
  numero: number;
  sql: string;
  nota: string | null;
  criadoEm: string;
  criadoPorNome: string | null;
};

export type UnidadeIndicador = { hospital: number; nome: string };
export type FonteIndicador = { id: string; nome: string };

/** Filtro do topo da página: unidade + período. */
export type FiltroIndicador = {
  hospital: number;
  inicio: string; // yyyy-MM-dd
  fim: string; // yyyy-MM-dd
};

/** Cadastro completo — meta e peso são cláusula de contrato e mudam sem deploy. */
export type SalvarIndicadorPayload = {
  aba: AbaIndicador;
  numero: string;
  ordem: number;
  indicadorPaiId: string | null;
  nome: string;
  memoriaCalculo: string | null;
  fonteDeclarada: string | null;
  meta: string | null;
  metaOperador: MetaOperador | null;
  metaValor: number | null;
  metaValorMaximo: number | null;
  pontuacao: number | null;
  tipoResultado: TipoResultadoIndicador;
  unidadeMedida: string | null;
  fatorDensidade: number | null;
  situacao: SituacaoIndicador;
  fonteId: string | null;
  sql: string | null;
  sqlAnalitico: string | null;
  ressalva: string | null;
  ativo: boolean;
  nota: string | null;
};

export const ABAS: { id: AbaIndicador; rotulo: string; rota: string }[] = [
  { id: 'Adulto', rotulo: 'Adulto', rota: 'adulto' },
  { id: 'Pediatrico', rotulo: 'Pediátrico', rota: 'pediatrico' },
  { id: 'MaternoInfantil', rotulo: 'Materno Infantil', rota: 'materno-infantil' },
  { id: 'PerfilEpidemiologico', rotulo: 'Perfil Epidemiológico', rota: 'perfil-epidemiologico' },
  { id: 'Institucional', rotulo: 'Institucional', rota: 'institucional' },
];

export const OPERADORES: { valor: MetaOperador; rotulo: string }[] = [
  { valor: 'MenorOuIgual', rotulo: '≤ (menor ou igual)' },
  { valor: 'MaiorOuIgual', rotulo: '≥ (maior ou igual)' },
  { valor: 'Menor', rotulo: '< (menor)' },
  { valor: 'Maior', rotulo: '> (maior)' },
  { valor: 'Igual', rotulo: '= (igual)' },
  { valor: 'Entre', rotulo: 'entre (faixa)' },
];

export const TIPOS: { valor: TipoResultadoIndicador; rotulo: string; contrato: string }[] = [
  {
    valor: 'Razao',
    rotulo: 'Razão (numerador ÷ denominador)',
    contrato: 'colunas numerador e denominador',
  },
  {
    valor: 'Densidade',
    rotulo: 'Densidade (× fator)',
    contrato: 'colunas numerador e denominador',
  },
  { valor: 'Absoluto', rotulo: 'Absoluto (contagem)', contrato: 'coluna numerador' },
  { valor: 'Distribuicao', rotulo: 'Distribuição', contrato: 'colunas rotulo e quantidade' },
  { valor: 'Agrupador', rotulo: 'Agrupador (soma dos filhos)', contrato: 'sem SQL' },
  { valor: 'Media', rotulo: 'Média (AVG pronto no SQL)', contrato: 'coluna valor (e denominador = amostra)' },
];

export function abaPorRota(rota: string | undefined): AbaIndicador | undefined {
  return ABAS.find((a) => a.rota === rota)?.id;
}

/**
 * Formata o resultado como a planilha: percentual vira %, densidade e média mostram a
 * unidade. A planilha guarda percentual como fração (0,85 = 85%), então é aqui que ×100
 * acontece — nunca no cálculo.
 */
export function formatarResultado(
  valor: number | null | undefined,
  unidade: string | null,
): string {
  if (valor === null || valor === undefined) return '—';

  if (unidade === '%') {
    return `${(valor * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  const numero = valor.toLocaleString('pt-BR', { maximumFractionDigits: 2 });
  return unidade ? `${numero} ${unidade}` : numero;
}
