// Modelo do questionário estruturado de laudo (estrutura_json do template) e das
// respostas marcadas no laudo. Genérico — BI-RADS é a 1ª "calculadora" plugada.

export type CategoriaBiRads = '0' | '1' | '2' | '3' | '4' | '4A' | '4B' | '4C' | '5' | '6';

export type Calculadora = 'BI-RADS' | 'OMS-DMO' | null;

/**
 * Classificação da densidade mineral óssea (OMS + posições oficiais SBDens/ISCD).
 * As três primeiras saem do T-score; as duas últimas do Z-score (crianças,
 * mulheres pré-menopausa e homens abaixo de 50 anos).
 */
export type ClassificacaoDmo =
  | 'NORMAL'
  | 'OSTEOPENIA'
  | 'OSTEOPOROSE'
  | 'DENTRO_ESPERADO'
  | 'ABAIXO_ESPERADO';

/** Escala usada como parâmetro do diagnóstico: T-score ou Z-score. */
export type EscalaDmo = 'T' | 'Z';

export type TipoCampo = 'opcao' | 'numero' | 'texto';

/** Campo preenchível dentro do texto de um item (substitui os `____` e as barras "direita/esquerda"). */
export type CampoItem = {
  /** Identificador usado no texto como `{chave}`. */
  chave: string;
  tipo: TipoCampo;
  /** Para tipo 'opcao': valores selecionáveis. */
  opcoes?: string[];
  /** Sufixo exibido após o valor (ex.: 'cm'). */
  sufixo?: string;
};

export type ItemChecklist = {
  id: string;
  /** Frase do laudo; pode conter `{chave}` para campos preenchíveis. */
  texto: string;
  /** Contribuição BI-RADS deste item (null = não influencia o cálculo). */
  birads?: CategoriaBiRads | null;
  campos?: CampoItem[];
};

export type SelecaoSecao = 'unica' | 'multipla';

/** Papel clínico de uma coluna — o que a calculadora deve ler dela. */
export type PapelColuna = 'tscore' | 'zscore';

export type TipoColunaTabela = 'texto' | 'numero';

/** Coluna de uma seção `tabela`. A `chave` indexa os valores em `RespostaItem.campos`. */
export type ColunaTabela = {
  chave: string;
  titulo: string;
  tipo: TipoColunaTabela;
  /**
   * Coluna de rótulo: o valor vem de `LinhaTabela.fixos` e não é editável no
   * preenchimento (ex.: "Região estudada" / "Sítio").
   */
  fixa?: boolean;
  /** Peso relativo da coluna na tabela do PDF (vira `style="width:N%"`). */
  larguraPct?: number;
  /** Sufixo exibido junto ao valor (ex.: '%'). */
  sufixo?: string;
  /** O que a calculadora lê desta coluna. */
  papel?: PapelColuna;
};

/** Linha pré-definida de uma seção `tabela` (os sítios medidos). */
export type LinhaTabela = {
  id: string;
  /** Valores das colunas `fixa` (rótulos da linha). */
  fixos: Record<string, string>;
  /**
   * Linha elegível como parâmetro do diagnóstico. Segundo a SBDens/ISCD são
   * coluna lombar e fêmur (colo ou total); o rádio 33% só em condições
   * específicas — por isso é opt-in, linha a linha.
   */
  diagnostica?: boolean;
  /**
   * Chaves das colunas `fixa` que, embora tenham rótulo-padrão em `fixos`, ficam
   * EDITÁVEIS no preenchimento do laudo (não só na autoria do template). Ex.: a
   * coluna lombar precisa permitir digitar o sítio ("L1 a L4", "L1-L2-L4") porque
   * às vezes se excluem vértebras. O valor digitado vive em `RespostaItem.campos`
   * (override); vazio = usa o rótulo de `fixos`.
   */
  fixosEditaveis?: string[];
};

export type SecaoChecklist = {
  id: string;
  titulo: string;
  /** 'unica' = radio (uma frase); 'multipla' = checkbox (várias frases). */
  selecao: SelecaoSecao;
  /**
   * Seção especial:
   * - 'birads'     → categoria BI-RADS calculada/editável (AVALIAÇÃO);
   * - 'tabela'     → grade de medidas (colunas tipadas × linhas fixas);
   * - 'oms-dmo'    → diagnóstico de densitometria calculado/editável;
   * - 'texto-fixo' → boilerplate que SEMPRE entra no laudo, sem marcação
   *   (critérios, notas de rodapé). Sem isso o texto teria de morar no editor
   *   livre — e qualquer mudança no checklist regenera o HTML e o apagaria.
   */
  tipo?: 'birads' | 'tabela' | 'oms-dmo' | 'texto-fixo';
  itens: ItemChecklist[];
  /** Só para `tipo: 'tabela'`. */
  colunas?: ColunaTabela[];
  linhas?: LinhaTabela[];
};

export type EstruturaChecklist = {
  versaoSchema: number;
  calculadora: Calculadora;
  secoes: SecaoChecklist[];
};

/**
 * Item marcado pela profissional + valores dos campos preenchidos.
 *
 * Numa seção `tabela` a mesma forma vale para uma LINHA: `itemId` é o id da
 * linha e `campos` são os valores das colunas (chave da coluna → valor).
 */
export type RespostaItem = {
  itemId: string;
  campos: Record<string, string>;
};

/**
 * Blob persistido em `laudo.respostas_checklist`. Guarda um SNAPSHOT da estrutura
 * (o template é mutável) para reabrir o painel de forma autossuficiente.
 */
export type RespostasChecklist = {
  estrutura: EstruturaChecklist;
  /** Itens marcados por seção (chave = secaoId). */
  marcados: Record<string, RespostaItem[]>;
  /** Override da profissional; null = usar o sugerido. */
  biRadsFinal: CategoriaBiRads | null;
  /** Override do diagnóstico de densitometria; null = usar o sugerido. */
  dmoFinal?: ClassificacaoDmo | null;
  /**
   * Escala do parâmetro na densitometria. T-score é o padrão (perimenopausa em
   * diante e homens ≥ 50); Z-score para crianças, pré-menopausa e homens < 50.
   */
  escalaDmo?: EscalaDmo;
};

export const VERSAO_SCHEMA_CHECKLIST = 1;

export function estruturaVazia(calculadora: Calculadora = 'BI-RADS'): EstruturaChecklist {
  return { versaoSchema: VERSAO_SCHEMA_CHECKLIST, calculadora, secoes: [] };
}
