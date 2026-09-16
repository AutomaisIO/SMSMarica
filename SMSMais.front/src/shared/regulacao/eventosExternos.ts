/**
 * Verbo tipado da trilha do SER/SERNIT (enum `TipoEventoExterno` do backend). O sistema externo
 * entrega o verbo como texto; o backend tipa uma vez, na captura, e o front só rotula.
 *
 * `Outro` é verbo que nenhuma regra reconheceu — a tela mostra o texto cru nesse caso.
 */
export type TipoEventoExterno =
  | 'Solicitar'
  | 'FollowUp'
  | 'Pendenciar'
  | 'Cancelar'
  | 'Agendar'
  | 'ChegadaNoDestino'
  | 'Transferir'
  | 'DevolvidoParaRegulacao'
  | 'WhatsApp'
  | 'Reagendar'
  | 'RetornarParaFila'
  | 'Alta'
  | 'CorrigirDados'
  | 'Outro';

export type EventoResumoExterno = {
  tipo: TipoEventoExterno;
  /** Verbo cru, como o sistema externo escreveu. */
  evento: string;
  dataEvento: string;
};

export const ROTULO_TIPO_EVENTO_EXTERNO: Record<TipoEventoExterno, string> = {
  Solicitar: 'Solicitada',
  FollowUp: 'FollowUP',
  Pendenciar: 'Pendenciada',
  Cancelar: 'Cancelada',
  Agendar: 'Agendada',
  ChegadaNoDestino: 'Chegada no destino',
  Transferir: 'Transferida',
  DevolvidoParaRegulacao: 'Devolvida para a regulação',
  WhatsApp: 'WhatsApp enviado pelo sistema',
  Reagendar: 'Reagendada',
  RetornarParaFila: 'Retornou para a fila',
  Alta: 'Alta',
  CorrigirDados: 'Dados corrigidos',
  Outro: 'Outro',
};

/** Ordem do filtro: o que pede reação da unidade primeiro, depois o fecho do ciclo. */
export const TIPOS_EVENTO_EXTERNO_FILTRO: TipoEventoExterno[] = [
  'DevolvidoParaRegulacao',
  'Transferir',
  'RetornarParaFila',
  'Pendenciar',
  'FollowUp',
  'WhatsApp',
  'Agendar',
  'Reagendar',
  'ChegadaNoDestino',
  'Alta',
  'Cancelar',
  'CorrigirDados',
  'Solicitar',
  'Outro',
];

/** Rótulo do tipo; para "Outro" devolve o verbo cru, que é a única informação que existe. */
export function rotuloTipoEventoExterno(tipo: TipoEventoExterno, eventoCru?: string | null): string {
  if (tipo === 'Outro' && eventoCru) return eventoCru;
  return ROTULO_TIPO_EVENTO_EXTERNO[tipo] ?? tipo;
}
