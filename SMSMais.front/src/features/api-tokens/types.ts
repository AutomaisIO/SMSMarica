export type ApiTokenListItem = {
  id: string;
  nome: string;
  prefixo: string;
  ativo: boolean;
  criadoEm: string;
  ultimoUsoEm: string | null;
  revogadoEm: string | null;
};

/** Resposta da criação — `token` em claro só vem nesta resposta, uma única vez. */
export type ApiTokenCriado = {
  id: string;
  nome: string;
  prefixo: string;
  token: string;
};
