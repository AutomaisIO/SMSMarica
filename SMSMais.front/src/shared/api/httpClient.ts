import axios, { AxiosError } from 'axios';
import { obterToken, obterUnidadeAtivaId, useAuth } from '@/shared/auth/authStore';
import { notificar } from '@/shared/ui/Notificacoes';
import { configurarSondaConexao, reportarFalhaDeConexao, type CausaQueda } from '@/shared/api/conexao';

const envBase = import.meta.env.VITE_API_BASE_URL?.trim();

// Em produção a env é OBRIGATÓRIA (ADR-0043 — uma instância por município). Até 2026-08 havia
// aqui um fallback para `https://api.smsmarica.online`: um build sem `VITE_API_BASE_URL` subia
// calado e apontava o painel de um município para o backend de Maricá. Falhar no build é
// barulhento e barato; descobrir isso em produção, não.
if (import.meta.env.PROD && !envBase) {
  throw new Error(
    'VITE_API_BASE_URL não definida. Cada instância aponta para o próprio backend — ' +
      'defina a variável no ambiente de build (ADR-0043).',
  );
}

// Em dev, o proxy `/api` do Vite resolve contra o backend local.
const baseURL = envBase || '/api';

export const http = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

configurarSondaConexao(baseURL);

/**
 * URL absoluta do backend — para passar a processos externos (ex.: o agente de
 * assinatura, lançado via protocolo). Em dev (`/api`) resolve contra a origem.
 * Defina `VITE_API_BASE_URL=http://localhost:5080` para o agente bater direto no backend.
 */
export const apiBaseAbsoluto: string = /^https?:\/\//i.test(baseURL)
  ? baseURL
  : typeof window !== 'undefined'
    ? new URL(baseURL, window.location.origin).toString()
    : baseURL;

http.interceptors.request.use((config) => {
  const token = obterToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  // Só injeta a unidade ativa quando a chamada não passou um X-Unidade-Id explícito.
  // Telas que operam sobre uma unidade-alvo diferente da ativa (ex.: cadastro de senha
  // do SISREG por unidade na Configuração/detalhe da unidade) mandam o header na própria
  // requisição e ele tem precedência.
  const unidadeId = obterUnidadeAtivaId();
  if (unidadeId && !config.headers['X-Unidade-Id']) {
    config.headers['X-Unidade-Id'] = unidadeId;
  }
  return config;
});

// Evita repetir o mesmo toast quando um retry (react-query) dispara o mesmo erro.
let ultimoAviso = { mensagem: '', em: 0 };
function avisarErroUnico(mensagem: string) {
  const agora = Date.now();
  if (mensagem === ultimoAviso.mensagem && agora - ultimoAviso.em < 4000) return;
  ultimoAviso = { mensagem, em: agora };
  notificar(mensagem, 'erro');
}

/** Leitura (GET/HEAD/OPTIONS) não muda nada no servidor; o resto é ação do usuário. */
function ehLeitura(erro: AxiosError): boolean {
  const metodo = (erro.config?.method ?? 'get').toLowerCase();
  return metodo === 'get' || metodo === 'head' || metodo === 'options';
}

/**
 * Falha que é de CONEXÃO, não de negócio: sem resposta nenhuma, ou o 503 que o nginx devolve no
 * lugar do 502 quando a API está fora (error_page do vhost, marcado com `servidorIndisponivel`).
 * O 503 do próprio backend (armazenamento indisponível) traz código de referência e NÃO entra aqui.
 */
function causaDeQueda(erro: AxiosError): CausaQueda | null {
  if (!erro.response) return 'servidor';
  const dados = erro.response.data as ProblemaApi | undefined;
  if (erro.response.status === 503 && dados?.servidorIndisponivel) return 'reiniciando';
  return null;
}

const MENSAGEM_ACAO_SEM_CONEXAO: Record<CausaQueda, string> = {
  internet: 'Sem internet neste computador — a ação não foi enviada. Tente de novo quando a conexão voltar.',
  reiniciando: 'O sistema está sendo atualizado agora — a ação pode não ter sido gravada. Confira e tente de novo em alguns segundos.',
  servidor: 'Não foi possível falar com o servidor — a ação pode não ter sido gravada. Confira e tente de novo.',
};

// Interceptor global de resposta:
// - 401: token inválido/expirado → limpa sessão e vai pro login (login nunca cai aqui).
// - Cancelamento (o próprio painel abortou a busca anterior, ou a tela fechou): silêncio — não é
//   falha. Era a origem de ~400 avisos falsos por dia na tela de Solicitações de Exame.
// - Queda de conexão: LEITURA não avisa na hora — abre a verificação pelo /health (conexao.ts),
//   que só mostra a faixa se a queda passar de 15s e refaz as consultas na volta. AÇÃO do usuário
//   (salvar, excluir…) avisa na hora: a pessoa precisa saber que talvez não tenha gravado.
// - >=500: SEMPRE avisa o usuário (rede de segurança contra 500 silencioso), com o código de
//   referência quando houver. Erros de negócio (4xx) NÃO são notificados aqui — cada tela os
//   trata inline.
http.interceptors.response.use(
  (r) => r,
  (erro: AxiosError) => {
    if (axios.isCancel(erro)) return Promise.reject(erro);

    const queda = causaDeQueda(erro);
    if (queda) {
      reportarFalhaDeConexao(queda);
      // O login mostra o erro na própria tela; "a ação pode não ter sido gravada" não cabe lá.
      if (!ehLeitura(erro) && !(erro.config?.url ?? '').includes('/identidade/login')) {
        const causa: CausaQueda = navigator.onLine === false ? 'internet' : queda;
        avisarErroUnico(MENSAGEM_ACAO_SEM_CONEXAO[causa]);
      }
      return Promise.reject(erro);
    }

    const status = erro.response?.status;
    if (status === 401) {
      const url = erro.config?.url ?? '';
      if (!url.includes('/identidade/login')) {
        const estado = useAuth.getState();
        if (estado.token || estado.usuario) {
          estado.sair();
          if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
            window.location.assign('/login');
          }
        }
      }
    } else if (status && status >= 500) {
      const dados = erro.response?.data as ProblemaApi | undefined;
      const msg = extrairMensagemDeErro(erro);
      // Dedup: o backend reusa o código quando o erro é idêntico a um já registrado.
      avisarErroUnico(dados?.jaReportado ? `Erro já reportado. ${msg}` : msg);
    }
    return Promise.reject(erro);
  },
);

export type ProblemaApi = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  /** Código de referência técnico interno (ex.: "ERRO-4F9C2A") em 500/503. */
  codigoReferencia?: string;
  /** true quando o backend deduplicou: erro idêntico já registrado antes. */
  jaReportado?: boolean;
  /** true no 503 que o NGINX devolve quando a API está fora (reiniciando) — não é erro do backend. */
  servidorIndisponivel?: boolean;
};

/** Código de referência do erro (500/503), se o backend o informou. */
export function extrairCodigoReferencia(erro: unknown): string | null {
  if (erro instanceof AxiosError) {
    const dados = erro.response?.data as ProblemaApi | undefined;
    return dados?.codigoReferencia ?? null;
  }
  return null;
}

export function extrairMensagemDeErro(erro: unknown): string {
  if (axios.isCancel(erro)) return 'Operação cancelada.';
  if (erro instanceof AxiosError) {
    // Sem resposta, o `message` do axios é "Network Error" — em inglês e sem dizer o que fazer.
    const queda = causaDeQueda(erro);
    if (queda === 'reiniciando') return 'O sistema está sendo atualizado. Tente de novo em alguns segundos.';
    if (queda) {
      return navigator.onLine === false
        ? 'Sem internet neste computador. Tente de novo quando a conexão voltar.'
        : 'Não foi possível falar com o servidor. Tente de novo em instantes.';
    }
    const dados = erro.response?.data as ProblemaApi | undefined;
    if (dados?.errors) {
      const msgs = Object.values(dados.errors).flat().filter(Boolean);
      if (msgs.length > 0) return msgs.join(' ');
    }
    let msg = dados?.detail || dados?.title || erro.message;
    // Garante o código na mensagem (caso o detail não o tenha embutido).
    const codigo = dados?.codigoReferencia;
    if (codigo && !msg.includes(codigo)) msg = `${msg} (código ${codigo})`;
    return msg;
  }
  if (erro instanceof Error) return erro.message;
  return 'Erro desconhecido.';
}
