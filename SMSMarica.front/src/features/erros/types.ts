/** Item de lista do log de erros (sem stack trace). */
export type RegistroErroListItem = {
  id: string;
  codigoReferencia: string;
  criadoEm: string;
  metodo: string;
  caminho: string;
  statusCode: number;
  tipoExcecao: string;
  mensagem: string;
  usuarioNome: string | null;
  ocorrencias: number;
  ultimaOcorrenciaEm: string;
  resolvidoEm: string | null;
};

/** Detalhe completo de um erro (com stack trace). */
export type RegistroErro = RegistroErroListItem & {
  queryString: string | null;
  stackTrace: string | null;
  interna: string | null;
  traceId: string | null;
  usuarioId: string | null;
  userAgent: string | null;
  resolvidoPor: string | null;
  resolucaoNota: string | null;
};

export type PaginaErros = {
  itens: RegistroErroListItem[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type ErroFiltro = {
  codigo?: string;
  texto?: string;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
};
