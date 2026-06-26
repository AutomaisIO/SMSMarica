import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type {
  AtualizarCredencialPayload,
  AtualizarProxyMotorPayload,
  AtualizarTfdGoogle,
  AtualizarTfdWhatsApp,
  IntegracaoCredencial,
  ProxyMotor,
  ProxyTesteCepResultado,
  ProxyTesteCpfResultado,
  ServicoProxy,
  TesteSpacesResultado,
  TfdGoogle,
  TfdWhatsApp,
} from '@/features/integracoes/types';

const keys = {
  credenciais: ['integracoes', 'credenciais'] as const,
  google: ['integracoes', 'tfd', 'google'] as const,
  whatsapp: ['integracoes', 'tfd', 'whatsapp'] as const,
  proxy: (servico: ServicoProxy) => ['integracoes', 'proxy', servico] as const,
};

// ---- Credenciais OAuth (store genérico) ----

async function listarCredenciais(): Promise<IntegracaoCredencial[]> {
  return (await http.get<IntegracaoCredencial[]>('/integracoes/credenciais')).data;
}

export function useCredenciais() {
  return useQuery({ queryKey: keys.credenciais, queryFn: listarCredenciais });
}

export function useSalvarCredencial() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { provedor: string; payload: AtualizarCredencialPayload }) =>
      http.put(`/integracoes/credenciais/${args.provedor}`, args.payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.credenciais }),
  });
}

export function useLimparCredencial() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (provedor: string) => http.delete(`/integracoes/credenciais/${provedor}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.credenciais }),
  });
}

// ---- DigitalOcean Spaces (S3) — teste de conexão ----

// Conecta, grava, lê e apaga um objeto de teste no bucket. Sempre retorna 200;
// o resultado do teste vem no corpo (`ok`/`etapa`/`mensagem`).
export async function testarSpaces(): Promise<TesteSpacesResultado> {
  return (
    await http.post<TesteSpacesResultado>('/integracoes/credenciais/digitalocean_spaces/testar')
  ).data;
}

export function useTestarSpaces() {
  return useMutation({ mutationFn: testarSpaces });
}

// ---- Google Maps (TFD) ----

export function useTfdGoogle() {
  return useQuery({
    queryKey: keys.google,
    queryFn: async () => (await http.get<TfdGoogle>('/integracoes/tfd/google')).data,
  });
}

export function useSalvarTfdGoogle() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarTfdGoogle) => http.put('/integracoes/tfd/google', payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.google }),
  });
}

// ---- WhatsApp / Meta (TFD) ----

export function useTfdWhatsApp() {
  return useQuery({
    queryKey: keys.whatsapp,
    queryFn: async () => (await http.get<TfdWhatsApp>('/integracoes/tfd/whatsapp')).data,
  });
}

export function useSalvarTfdWhatsApp() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarTfdWhatsApp) => http.put('/integracoes/tfd/whatsapp', payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.whatsapp }),
  });
}

// ---- Motores de proxy (CPF/CEP) com fallback ----

export function useProxyMotores(servico: ServicoProxy) {
  return useQuery({
    queryKey: keys.proxy(servico),
    queryFn: async () =>
      (await http.get<ProxyMotor[]>(`/integracoes/proxy/${servico}`)).data,
  });
}

export function useSalvarProxyMotor(servico: ServicoProxy) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (args: { motor: string; payload: AtualizarProxyMotorPayload }) =>
      http.put(`/integracoes/proxy/${servico}/${args.motor}`, args.payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.proxy(servico) }),
  });
}

// Testa um motor específico (sem fallback). Sempre HTTP 200; sucesso/erro no corpo.
export function useTestarProxyCpf(motor: string) {
  return useMutation({
    mutationFn: async (body: { cpf: string; dataNascimento: string }) =>
      (await http.post<ProxyTesteCpfResultado>(`/integracoes/proxy/cpf/${motor}/testar`, body)).data,
  });
}

export function useTestarProxyCep(motor: string) {
  return useMutation({
    mutationFn: async (body: { cep: string }) =>
      (await http.post<ProxyTesteCepResultado>(`/integracoes/proxy/cep/${motor}/testar`, body)).data,
  });
}
