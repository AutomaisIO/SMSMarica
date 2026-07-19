import { create } from 'zustand';
import { http } from '@/shared/api/httpClient';

export type ModuloPermissao =
  | 'Pacientes'
  | 'Unidades'
  | 'Veiculos'
  | 'Motoristas'
  | 'Usuarios'
  | 'TiposTratamento'
  | 'Perfis'
  | 'Tratamentos'
  | 'Translados'
  | 'Rastreamento'
  | 'Avaliacoes'
  | 'Pacs'
  | 'Medicos'
  | 'Laudos'
  | 'LaudosTemplates'
  | 'SolicitacoesExame'
  | 'TiposExame'
  | 'ProcedimentosSigtap'
  | 'Inteligencia'
  | 'InteligenciaConfiguracao'
  | 'InteligenciaAprendizado'
  | 'Especialidades'
  | 'Equipamentos'
  | 'Agendamentos'
  | 'Sisreg'
  | 'SisregConfiguracao'
  | 'SincronizacaoPep'
  | 'ApiTokens'
  | 'Cidadao'
  | 'IntegracoesConfig'
  | 'Faturamento'
  | 'ConfiguracaoLaudo'
  | 'Auditoria'
  | 'Erros'
  | 'Conversas'
  | 'ConversasSupervisao'
  | 'Ticket'
  | 'NotificacoesAgendamento'
  | 'Sandbox'
  | 'RespostasRapidas'
  | 'Consultas'
  | 'MapeamentoSigtap'
  | 'AgenteIa';

export type AcaoPermissao = 'Consulta' | 'Inclusao' | 'Edicao' | 'Exclusao';

export type UsuarioAutenticado = {
  id: string;
  nome: string;
  email: string;
  deveTrocarSenha: boolean;
  /** Papel derivado no hub FHIR: "Medico", "Motorista"… (null para usuário comum). */
  papelAtual?: string;
};

type PermissaoApi = { modulo: ModuloPermissao; acoes: string };

/** Unidade vinculada ao usuário (usuario_unidade), retornada no login. */
export type UnidadeVinculada = { id: string; nome: string; principal: boolean };

type AuthState = {
  usuario: UsuarioAutenticado | null;
  token: string | null;
  expiraEm: string | null;
  permissoes: Partial<Record<ModuloPermissao, AcaoPermissao[]>>;
  unidades: UnidadeVinculada[];
  /** Unidade ativa da sessão (enviada em X-Unidade-Id). null = sem seleção (todas as vinculadas). */
  unidadeAtivaId: string | null;
  entrar: (credenciais: { email: string; senha: string }) => Promise<void>;
  sair: () => void;
  recarregarPermissoes: () => Promise<void>;
  /** Marca que a troca obrigatória foi concluída (limpa a flag local). */
  marcarSenhaTrocada: () => void;
  definirUnidadeAtiva: (id: string | null) => void;
};

const CHAVE_STORAGE = 'smsmarica.auth';

type Persistido = {
  usuario: UsuarioAutenticado;
  token: string;
  expiraEm: string;
  permissoes: Partial<Record<ModuloPermissao, AcaoPermissao[]>>;
  unidades?: UnidadeVinculada[];
  unidadeAtivaId?: string | null;
};

function lerPersistido(): Persistido | null {
  try {
    const bruto = localStorage.getItem(CHAVE_STORAGE);
    if (!bruto) return null;
    const obj = JSON.parse(bruto) as Persistido;
    // Se já expirou, descarta para forçar novo login.
    if (obj?.expiraEm && new Date(obj.expiraEm).getTime() < Date.now()) return null;
    // Sessões antigas não têm os campos de unidade; normaliza e valida a ativa.
    obj.unidades = obj.unidades ?? [];
    if (obj.unidadeAtivaId && !obj.unidades.some((u) => u.id === obj.unidadeAtivaId)) {
      obj.unidadeAtivaId = null;
    }
    return obj;
  } catch {
    return null;
  }
}

/** Unidade ativa default: única vinculada, senão a principal, senão null (seletor no login). */
function unidadeAtivaDefault(unidades: UnidadeVinculada[]): string | null {
  if (unidades.length === 1) return unidades[0].id;
  return unidades.find((u) => u.principal)?.id ?? null;
}

function parseAcoes(s: string): AcaoPermissao[] {
  if (!s || s === 'Nenhuma') return [];
  if (s === 'Todas') return ['Consulta', 'Inclusao', 'Edicao', 'Exclusao'];
  return s
    .split(/\s*,\s*/)
    .filter(Boolean)
    .filter((x): x is AcaoPermissao =>
      x === 'Consulta' || x === 'Inclusao' || x === 'Edicao' || x === 'Exclusao',
    );
}

function indexarPermissoes(lista: PermissaoApi[] | undefined | null) {
  const result: Partial<Record<ModuloPermissao, AcaoPermissao[]>> = {};
  for (const p of lista ?? []) {
    result[p.modulo] = parseAcoes(p.acoes);
  }
  return result;
}

type LoginResposta = {
  token: string;
  expiraEm: string;
  usuario: {
    id: string;
    nomeCompleto: string;
    email: string;
    deveTrocarSenha: boolean;
    papelAtual?: string | null;
  };
  permissoes: PermissaoApi[];
  unidades?: { id: string; nome: string; principal: boolean }[];
};

const persistido = lerPersistido();

/** Regrava a sessão persistida a partir do estado atual (no-op se não autenticado). */
function persistirEstado(s: Pick<AuthState, 'usuario' | 'token' | 'expiraEm' | 'permissoes' | 'unidades' | 'unidadeAtivaId'>) {
  if (!s.usuario || !s.token || !s.expiraEm) return;
  const dados: Persistido = {
    usuario: s.usuario,
    token: s.token,
    expiraEm: s.expiraEm,
    permissoes: s.permissoes,
    unidades: s.unidades,
    unidadeAtivaId: s.unidadeAtivaId,
  };
  localStorage.setItem(CHAVE_STORAGE, JSON.stringify(dados));
}

export const useAuth = create<AuthState>((set, get) => ({
  usuario: persistido?.usuario ?? null,
  token: persistido?.token ?? null,
  expiraEm: persistido?.expiraEm ?? null,
  permissoes: persistido?.permissoes ?? {},
  unidades: persistido?.unidades ?? [],
  unidadeAtivaId: persistido?.unidadeAtivaId ?? null,

  entrar: async ({ email, senha }) => {
    const { data } = await http.post<LoginResposta>('/identidade/login', { email, senha });
    const usuario: UsuarioAutenticado = {
      id: data.usuario.id,
      nome: data.usuario.nomeCompleto,
      email: data.usuario.email,
      deveTrocarSenha: data.usuario.deveTrocarSenha,
      papelAtual: data.usuario.papelAtual ?? undefined,
    };
    const permissoes = indexarPermissoes(data.permissoes);
    const unidades: UnidadeVinculada[] = data.unidades ?? [];
    const unidadeAtivaId = unidadeAtivaDefault(unidades);
    const novo = { usuario, token: data.token, expiraEm: data.expiraEm, permissoes, unidades, unidadeAtivaId };
    persistirEstado(novo);
    set(novo);
  },

  sair: () => {
    localStorage.removeItem(CHAVE_STORAGE);
    set({ usuario: null, token: null, expiraEm: null, permissoes: {}, unidades: [], unidadeAtivaId: null });
  },

  definirUnidadeAtiva: (id) => {
    const atual = get();
    const valido = id !== null && atual.unidades.some((u) => u.id === id) ? id : null;
    set({ unidadeAtivaId: valido });
    persistirEstado({ ...atual, unidadeAtivaId: valido });
  },

  marcarSenhaTrocada: () => {
    const atual = get();
    if (!atual.usuario) return;
    const novo: UsuarioAutenticado = { ...atual.usuario, deveTrocarSenha: false };
    persistirEstado({ ...atual, usuario: novo });
    set({ usuario: novo });
  },

  recarregarPermissoes: async () => {
    const usuario = get().usuario;
    if (!usuario) return;
    type Resp = { resolvidas: PermissaoApi[] };
    const { data } = await http.get<Resp>('/identidade/me/permissoes');
    const permissoes = indexarPermissoes(data.resolvidas);
    persistirEstado({ ...get(), permissoes });
    set({ permissoes });
  },
}));

export function obterToken(): string | null {
  return useAuth.getState().token;
}

/** Unidade ativa da sessão — lida pelo interceptor HTTP (header X-Unidade-Id). */
export function obterUnidadeAtivaId(): string | null {
  return useAuth.getState().unidadeAtivaId;
}

/** Hook utilitário: o usuário pode CONSULTAR (entrar em) o módulo? */
export function useTemConsulta(modulo: ModuloPermissao): boolean {
  return useAuth((s) => (s.permissoes[modulo] ?? []).includes('Consulta'));
}

/** Hook utilitário: o usuário pode executar a ação específica no módulo? */
export function usePermissao(modulo: ModuloPermissao, acao: AcaoPermissao): boolean {
  return useAuth((s) => (s.permissoes[modulo] ?? []).includes(acao));
}

/** Hook utilitário: o usuário logado é médico (papel derivado do hub FHIR)? */
export function useEhMedico(): boolean {
  return useAuth((s) => s.usuario?.papelAtual === 'Medico');
}
