// Chat de dados (menu "Consulta Inteligente"). Cada pergunta é uma sessão conversável,
// restrita a consultar a base escolhida. O formato de evento é contrato com o motor Python.

export type EventoConsulta =
  | { seq: number; type: 'text'; text: string }
  | { seq: number; type: 'thinking'; text: string }
  | { seq: number; type: 'tool_use'; id?: string; name: string; input?: unknown }
  | { seq: number; type: 'tool_result'; id?: string; isError?: boolean; content?: unknown }
  | { seq: number; type: 'result'; text?: string; isError?: boolean }
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
  events: EventoConsulta[];
  usuario_nome: string | null;
};

export type SessaoResumo = {
  id: string;
  title: string;
  base_slug: string | null;
  created_at: number;
  last_used_at: number;
  archived_at: number | null;
  turn_count: number;
  running: boolean;
  usuario_nome: string | null;
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
  events: EventoConsulta[];
  cursor: number;
  partial: string | null;
};

/** Base consultável (fonte). Reusa GET /ia/fontes. */
export type FonteConsulta = {
  id: string;
  nome: string;
  tipo: string;
  ambiente: string;
  slug: string;
};

// ── Especificação de visualização (tool `visualizar` do motor) ──────────────────────────
// O agente chama mcp__dados__visualizar com { spec }. O front lê o input do tool_use e
// renderiza. Tipos de gráfico saem agora; mapas Google entram quando houver a chave.

export type PontoMapa = { lat: number; lng: number; rotulo?: string; valor?: number; peso?: number };
export type PoligonoMapa = { coordenadas: [number, number][]; rotulo?: string; valor?: number };
export type ParCategoria = { rotulo: string; valor: number };

export type VizSpec = {
  tipo:
    | 'numero'
    | 'tabela'
    | 'pizza'
    | 'barra'
    | 'linha'
    | 'mapa_pontos'
    | 'mapa_calor'
    | 'mapa_poligono';
  titulo?: string;
  // número
  valor?: number | string;
  unidade?: string;
  // tabela
  colunas?: string[];
  linhas?: unknown[][];
  // gráficos
  eixoX?: string;
  eixoY?: string;
  dados?: ParCategoria[];
  // mapas
  pontos?: PontoMapa[];
  poligonos?: PoligonoMapa[];
};
