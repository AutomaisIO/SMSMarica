import axios, { AxiosError } from 'axios';
import { obterToken, obterUnidadeAtivaId, useAuth } from '@/shared/auth/authStore';
import { notificar } from '@/shared/ui/Notificacoes';

const URL_PROD = 'https://api.smsmarica.online';
const envBase = import.meta.env.VITE_API_BASE_URL?.trim();
// Ordem: env var explícita > fallback prod em build de produção > proxy /api em dev.
const baseURL = envBase || (import.meta.env.PROD ? URL_PROD : '/api');

export const http = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

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

// Interceptor global de resposta:
// - 401: token inválido/expirado → limpa sessão e vai pro login (login nunca cai aqui).
// - >=500 ou erro de rede: SEMPRE avisa o usuário (rede de segurança contra 500 silencioso),
//   com o código de referência quando houver. Erros de negócio (4xx) NÃO são notificados
//   aqui — cada tela os trata inline.
http.interceptors.response.use(
  (r) => r,
  (erro: AxiosError) => {
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
    } else if (!erro.response) {
      // Sem resposta = rede/timeout/CORS/servidor fora. Não expõe infra.
      avisarErroUnico('Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.');
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
  if (erro instanceof AxiosError) {
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
