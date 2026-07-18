/** Quem executou a ação: usuário do sistema (equipe) ou paciente (app do cidadão). */
export type TipoAtorAuditoria = 'Desconhecido' | 'UsuarioSistema' | 'Paciente';

/** Uma linha da trilha de auditoria (espelha RegistroAuditoriaDto do backend). */
export type RegistroAuditoria = {
  id: string;
  entidade: string;
  entidadeId: string;
  acao: string;
  valorAnterior: string | null;
  valorNovo: string | null;
  usuarioId: string | null;
  /** Nome do ator resolvido (usuário do sistema OU paciente). */
  usuarioNome: string | null;
  /** Tipo do ator — o "quem é". */
  atorTipo: TipoAtorAuditoria;
  /** Nome da entidade afetada (o paciente cujo cadastro mudou). */
  entidadeNome: string | null;
  /** IP de onde partiu (null em registros antigos). */
  ip: string | null;
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
