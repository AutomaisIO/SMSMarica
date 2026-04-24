import axios, { AxiosError } from 'axios';
import { obterTokenMock } from '@/shared/auth/authStore';

const URL_PROD = 'https://api.smsmarica.online';
const envBase = import.meta.env.VITE_API_BASE_URL?.trim();
// Ordem: env var explícita > fallback prod em build de produção > proxy /api em dev.
const baseURL = envBase || (import.meta.env.PROD ? URL_PROD : '/api');

export const http = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

http.interceptors.request.use((config) => {
  const token = obterTokenMock();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

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
