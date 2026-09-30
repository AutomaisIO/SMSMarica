/**
 * Indicadores de Regulação — série mensal por sistema (SISREG, SER, SERNIT, ESUS São Gonçalo).
 *
 * Espelha o contrato de `GET /regulacao/indicadores/{fonte}` (módulo IndicadoresRegulacao, 79). É o
 * mesmo formato do relatório em PDF de 30/09/2026 (`docs/regulacao/relatorio-2025-2026/dados.json`),
 * com as chaves em camelCase e os enums viajando como string.
 */

/** Como a fonte vai na URL (da tela e da API). */
export type FonteIndicadores = 'sisreg' | 'ser' | 'sernit' | 'esussg';

/** Como a fonte volta no corpo da resposta (enum do backend). */
export type Fonte = 'Sisreg' | 'Ser' | 'Sernit' | 'EsusSg';

/** Selo de origem — é o que impede um número parcial de ser lido como oficial. */
export type Selo = 'Oficial' | 'Calculado' | 'Parcial' | 'Indisponivel';

export type Formato = 'Inteiro' | 'Percentual' | 'Dias';

/** Como fechar o bloco anual quando a série não traz `anual[ano]`. */
export type Agregacao = 'Soma' | 'UltimoMes' | 'Media';

export interface SerieIndicador {
  rotulo: string;
  selo: Selo;
  nota: string | null;
  formato: Formato;
  /** Linha principal da seção: vai em negrito. */
  destaque: boolean;
  /** Detalhamento da linha de cima: vai recuado. */
  subitem: boolean;
  /** `'AAAA-MM'` → valor do mês (nulo = sem dado). */
  valores: Record<string, number | null>;
  /**
   * `'AAAA'` → valor do ano calculado do jeito certo (razão de somas, mediana de todos os casos).
   * Somar ou tirar média de meses distorce percentual e tempo; quando vier, vale este.
   */
  anual: Record<string, number | null> | null;
  agregacao: Agregacao;
}

export interface TabelaIndicador {
  titulo: string;
  colunas: string[];
  /** Células já formatadas pelo backend (nulo = "—"). */
  linhas: (string | null)[][];
  nota: string | null;
}

export interface GraficoIndicador {
  tipo: 'barras' | 'empilhado' | string;
  /** Rótulos das séries da mesma seção que entram no gráfico, na ordem das cores. */
  series: string[];
}

export interface ResumoTempo {
  n: number;
  mediana: number | null;
  p90: number | null;
  menor: number | null;
  maior: number | null;
}

export type IdSecao = 'vagas' | 'absenteismo' | 'regulados' | 'fila' | 'desfechos' | 'espera' | 'judicial';

export interface SecaoIndicador {
  id: IdSecao | string;
  titulo: string;
  texto: string | null;
  /** O sistema de origem não fornece nada desta seção ao município. */
  indisponivel: boolean;
  series: SerieIndicador[];
  grafico: GraficoIndicador | null;
  motivos: TabelaIndicador | null;
  tabelas: TabelaIndicador[];
  resumoJudicial: ResumoTempo | null;
}

export interface IndicadoresRegulacao {
  fonte: Fonte;
  sistema: string;
  nomeSistema: string;
  /** Meses do período, `'AAAA-MM'`, em ordem. */
  meses: string[];
  geradoEm: string;
  cobertura: string[];
  secoes: SecaoIndicador[];
}

/** Período pedido: `'AAAA-MM'`. Vazio nos dois = o padrão do backend (últimos 12 meses fechados). */
export type PeriodoMeses = { inicio: string; fim: string };
