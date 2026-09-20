import axios from 'axios';
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
  | 'SolicitacaoExameManual'
  | 'TiposExame'
  | 'ProcedimentosSigtap'
  | 'Inteligencia'
  | 'InteligenciaConfiguracao'
  | 'InteligenciaAprendizado'
  | 'InteligenciaConsultaDev'
  | 'Equipamentos'
  | 'Sisreg'
  | 'SisregConfiguracao'
  | 'SisregMapeamento'
  | 'RegulacaoSer'
  | 'CorrecaoIdentidadeExame'
  | 'PesquisaSatisfacao'
  | 'Instituicao'
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
  | 'AgenteIa'
  | 'Indicadores'
  | 'Estatistica'
  // Processo Regulatório (47–51). O módulo em si ainda não existe no front; os três de VISÃO
  // GLOBAL já valem hoje: são eles que liberam a lente "Município" do painel de início (ADR-0033).
  | 'Regulacao'
  | 'RegulacaoTriagem'
  | 'RegulacaoMedica'
  | 'RegulacaoAgendamento'
  | 'RegulacaoConfiguracao'
  | 'RegulacaoSernit'
  | 'RoboAtendimento'
  | 'AjusteCadastro'
  | 'AlteracoesAgenda'
  | 'Agenda'
  | 'RevelarChaveSisreg'
  | 'InteligenciaAtendimento'
  | 'Confirmacoes'
  | 'EstrategiasFila'
  | 'EstatisticaCustos'
  // Estatísticas dos operadores por sistema de regulação (68–70): três módulos, um por sistema.
  | 'EstatisticaSisreg'
  | 'EstatisticaSer'
  | 'EstatisticaSernit';

export type AcaoPermissao = 'Consulta' | 'Inclusao' | 'Edicao' | 'Exclusao';

export type UsuarioAutenticado = {
  id: string;
  nome: string;
  email: string;
  deveTrocarSenha: boolean;
  /** Papel derivado no hub FHIR: "Medico", "Motorista"… (null para usuário comum). */
  papelAtual?: string;
  /**
   * Enxerga todas as unidades. Também é quem pode conceder isso a outro. Opcional porque
   * sessão gravada antes deste campo existir volta do localStorage sem ele.
   */
  acessoGlobal?: boolean;
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
    acessoGlobal?: boolean;
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
      acessoGlobal: data.usuario.acessoGlobal ?? false,
    };
    const permissoes = indexarPermissoes(data.permissoes);
    const unidades: UnidadeVinculada[] = data.unidades ?? [];
    const unidadeAtivaId = unidadeAtivaDefault(unidades);
    const novo = { usuario, token: data.token, expiraEm: data.expiraEm, permissoes, unidades, unidadeAtivaId };
    persistirEstado(novo);
    set(novo);
  },

  sair: () => {
    // A sessão de ESCRITA no SER (e no SERNIT) morre junto. A senha de cada operador vive na
    // memória do servidor amarrada a ESTA sessão (o `jti` do token), e "sair" tem de significar
    // sair de todas — senão a credencial pessoal dele no sistema do Estado (ou de Niterói)
    // continuaria viva por horas.
    const { token } = get();
    if (token) {
      void encerrarSessaoDeEscritaNoSer(token);
      void encerrarSessaoDeEscritaNoSernit(token);
    }

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

/**
 * Encerra a sessão de escrita no SER durante o logout.
 *
 * Usa uma instância CRUA do axios de propósito: o interceptor global reage a 401 chamando
 * `sair()`, e esta chamada acontece dentro do próprio `sair()`. E manda o token explicitamente
 * porque o estado está prestes a ser limpo.
 */
async function encerrarSessaoDeEscritaNoSer(token: string): Promise<void> {
  try {
    await axios.delete('/regulacao/ser/sessao', {
      baseURL: http.defaults.baseURL,
      headers: { Authorization: `Bearer ${token}` },
      timeout: 5000,
    });
  } catch {
    // Sair do sistema não pode falhar porque o SER (ou a rede) não respondeu. Uma sessão órfã
    // ainda cai sozinha pela validade por inatividade do servidor.
  }
}

/** Igual à do SER, para a sessão de escrita no SERNIT (SER de Niterói). Ver comentário acima. */
async function encerrarSessaoDeEscritaNoSernit(token: string): Promise<void> {
  try {
    await axios.delete('/regulacao/sernit/sessao', {
      baseURL: http.defaults.baseURL,
      headers: { Authorization: `Bearer ${token}` },
      timeout: 5000,
    });
  } catch {
    // Sair do sistema não pode falhar porque o SERNIT (ou a rede) não respondeu.
  }
}
