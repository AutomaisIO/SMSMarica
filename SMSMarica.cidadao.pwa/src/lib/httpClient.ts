import axios, { AxiosError } from 'axios';
import { obterToken, useAuth } from '@/store/auth';

const URL_PROD = 'https://api.smsmarica.online';
const envBase = import.meta.env.VITE_API_BASE_URL?.trim();
// Ordem: env var explícita > fallback prod em build de produção > proxy /api em dev.
const baseURL = envBase || (import.meta.env.PROD ? URL_PROD : '/api');

export const http = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

http.interceptors.request.use((config) => {
  const token = obterToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

http.interceptors.response.use(
  (r) => r,
  (erro: AxiosError) => {
    if (erro.response?.status === 401) {
      const url = erro.config?.url ?? '';
      // 401 nos endpoints de login (OTP) significa "código inválido" — NÃO deslogar.
      // Em qualquer outro endpoint autenticado, 401 = sessão expirada/revogada
      // (inclui /consentimento): limpa a sessão e manda pro login. Sem isso, o app
      // ficava preso na tela de consentimento no iOS (token velho persistido).
      const ehLogin = url.includes('-otp');
      const estado = useAuth.getState();
      if (!ehLogin && estado.token) {
        estado.sair();
        if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
          window.location.assign('/login');
        }
      }
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
};

/**
 * Status HTTP + código de negócio (o `type` do ProblemDetails, ex.: "confirmacao.ja_respondida")
 * de um erro da API. Serve para a tela escolher um texto AMIGÁVEL — nunca exibimos ao
 * cidadão a mensagem crua vinda do servidor.
 */
export function classificarErro(erro: unknown): { status?: number; codigo?: string } {
  if (!(erro instanceof AxiosError)) return {};
  const dados = erro.response?.data as ProblemaApi | undefined;
  return { status: erro.response?.status, codigo: dados?.type ?? undefined };
}

export function extrairMensagemDeErro(erro: unknown): string {
  if (erro instanceof AxiosError) {
    const dados = erro.response?.data as ProblemaApi | undefined;
    if (dados?.errors) {
      const msgs = Object.values(dados.errors).flat().filter(Boolean);
      if (msgs.length > 0) return msgs.join(' ');
    }
    if (dados?.detail) return dados.detail;
    if (dados?.title) return dados.title;
    return erro.message;
  }
  if (erro instanceof Error) return erro.message;
  return 'Erro desconhecido.';
}
