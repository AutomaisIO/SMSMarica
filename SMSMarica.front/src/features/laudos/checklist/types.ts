// Modelo do questionário estruturado de laudo (estrutura_json do template) e das
// respostas marcadas no laudo. Genérico — BI-RADS é a 1ª "calculadora" plugada.

export type CategoriaBiRads = '0' | '1' | '2' | '3' | '4' | '4A' | '4B' | '4C' | '5' | '6';

export type Calculadora = 'BI-RADS' | null;

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

export type SecaoChecklist = {
  id: string;
  titulo: string;
  /** 'unica' = radio (uma frase); 'multipla' = checkbox (várias frases). */
  selecao: SelecaoSecao;
  /** Seção especial: 'birads' renderiza a categoria calculada/editável (AVALIAÇÃO). */
  tipo?: 'birads';
  itens: ItemChecklist[];
};

export type EstruturaChecklist = {
  versaoSchema: number;
  calculadora: Calculadora;
  secoes: SecaoChecklist[];
};

/** Item marcado pela profissional + valores dos campos preenchidos. */
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
};

export const VERSAO_SCHEMA_CHECKLIST = 1;

export function estruturaVazia(calculadora: Calculadora = 'BI-RADS'): EstruturaChecklist {
  return { versaoSchema: VERSAO_SCHEMA_CHECKLIST, calculadora, secoes: [] };
}
