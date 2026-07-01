/** Uma linha da trilha de auditoria (espelha RegistroAuditoriaDto do backend). */
export type RegistroAuditoria = {
  id: string;
  entidade: string;
  entidadeId: string;
  acao: string;
  valorAnterior: string | null;
  valorNovo: string | null;
  usuarioId: string | null;
  usuarioNome: string | null;
  criadoEm: string;
};

export type PaginaAuditoria = {
  itens: RegistroAuditoria[];
  total: number;
  pagina: number;
  tamanho: number;
};

export type AuditoriaFiltro = {
  entidade?: string;
  entidadeId?: string;
  usuarioId?: string;
  texto?: string;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
};
