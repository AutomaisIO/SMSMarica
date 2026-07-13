export type TipoCampoResposta = 'Texto' | 'Data' | 'Numero';

/** Variável MANUAL: o operador preenche ao usar o atalho. */
export type RespostaRapidaCampo = {
  nome: string;
  rotulo: string | null;
  tipo: TipoCampoResposta;
  ordem: number;
};

/** Mensagem pronta do chat. `unidadeId` nulo = vale para toda a SMS. */
export type RespostaRapida = {
  id: string;
  titulo: string;
  corpo: string;
  categoria: string | null;
  unidadeId: string | null;
  unidadeNome: string | null;
  ativo: boolean;
  ordem: number;
  campos: RespostaRapidaCampo[];
  /** Tags do corpo que o servidor resolve sozinho — a UI mostra, mas não pede. */
  tagsAutomaticas: string[];
};

export type SalvarRespostaRapidaPayload = {
  titulo: string;
  corpo: string;
  categoria: string | null;
  unidadeId: string | null;
  ativo: boolean;
  ordem: number;
  campos: RespostaRapidaCampo[];
};

/** Tag que o sistema preenche a partir da conversa (não vira campo do formulário). */
export type TagAutomatica = {
  nome: string;
  descricao: string;
};

export type TextoResolvido = {
  texto: string;
  /** Tags que ficaram sem valor — continuam visíveis como {{tag}} no texto. */
  pendentes: string[];
};

export const ROTULO_TIPO_CAMPO: Record<TipoCampoResposta, string> = {
  Texto: 'Texto',
  Data: 'Data',
  Numero: 'Número',
};
