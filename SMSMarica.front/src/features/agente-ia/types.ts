/** Eventos emitidos pelo motor durante um turno. O formato é contrato com o serviço Python. */
export type EventoAgente =
  | { seq: number; type: 'text'; text: string }
  | { seq: number; type: 'thinking'; text: string }
  | { seq: number; type: 'tool_use'; id?: string; name: string; input?: unknown }
  | { seq: number; type: 'tool_result'; id?: string; isError?: boolean; content?: unknown }
  | { seq: number; type: 'result'; text?: string; isError?: boolean; numTurns?: number; costUsd?: number }
  | { seq: number; type: 'unknown'; raw?: string };

export type StatusTurno = 'running' | 'done' | 'error' | 'cancelled' | 'interrupted';

export type TurnoResumo = {
  id: string;
  session_id: string;
  prompt: string;
  status: StatusTurno;
  error: string | null;
  started_at: number;
  finished_at: number | null;
  events: EventoAgente[];
};

export type SessaoResumo = {
  id: string;
  title: string;
  ticket_numero: number | null;
  ticket_titulo: string | null;
  created_at: number;
  last_used_at: number;
  turn_count: number;
  running: boolean;
};

export type SessaoDetalhe = SessaoResumo & {
  turns: TurnoResumo[];
  currentTurnId: string | null;
};

export type TurnoView = {
  turnId: string;
  sessionId: string;
  status: StatusTurno;
  error: string | null;
  events: EventoAgente[];
  cursor: number;
};

export type CriarSessaoPayload = {
  title?: string;
  /** Preenchido quando a sessão é aberta a partir de um ticket. */
  ticketNumero?: number;
  ticketTitulo?: string;
};
