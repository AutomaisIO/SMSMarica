import axios, { AxiosError } from 'axios';
import { obterToken, useAuth } from '@/shared/auth/authStore';

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
  return config;
});

// Em 401 (token inválido/expirado), limpa a sessão e manda para o login.
// Login bem-sucedido nunca cai aqui (o erro vem com 400/422).
http.interceptors.response.use(
  (r) => r,
  (erro: AxiosError) => {
    if (erro.response?.status === 401) {
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
