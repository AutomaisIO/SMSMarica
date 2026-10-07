/**
 * Recurso das Notificações da regulação: o procedimento/especialidade que a solicitação pede
 * (ex.: "CONSULTA EM OFTALMOLOGIA - PEDIATRIA"). Vem do sistema externo como está, e é a chave do
 * filtro — não se normaliza (ao contrário do técnico): a opção devolve o mesmo valor que o filtro
 * compara.
 */

/** Uma opção do filtro por recurso: o nome, o tipo (Consulta/Exame — para agrupar/colorir) e
 * quantas notificações pendentes são desse recurso agora (0 é legítimo: o catálogo é completo). */
export type RecursoNotificacao = {
  recurso: string;
  /** "Consulta" | "Exame" (como o backend serializa TipoRecursoSer). */
  tipo: string;
  pendentes: number;
};
