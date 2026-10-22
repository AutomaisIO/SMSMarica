import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type {
  AtualizarCredencialPayload,
  AtualizarElevenLabs,
  AtualizarProxyMotorPayload,
  AtualizarTfdGoogle,
  AtualizarTfdWhatsApp,
  ElevenLabs,
  VozElevenLabs,
  VozBibliotecaElevenLabs,
  FiltroVozBiblioteca,
  IntegracaoCredencial,
  ProxyMotor,
  ProxyTesteCepResultado,
  ProxyTesteCpfResultado,
  ServicoProxy,
  TesteFcmResultado,
  TesteSpacesResultado,
  TfdGoogle,
  TfdWhatsApp,
} from '@/features/integracoes/types';

const keys = {
  credenciais: ['integracoes', 'credenciais'] as const,
  google: ['integracoes', 'tfd', 'google'] as const,
  whatsapp: ['integracoes', 'tfd', 'whatsapp'] as const,
  elevenlabs: ['integracoes', 'elevenlabs'] as const,
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

// ---- Firebase (FCM) — notificações do app do cidadão ----

// Obtém um token de acesso do Google com a conta de serviço gravada e faz um envio de validação
// (validate_only: confere API ligada e permissão de envio); nenhuma notificação é entregue.
// Sempre retorna 200; o resultado do teste vem no corpo (`ok`/`mensagem`).
export async function testarFcm(): Promise<TesteFcmResultado> {
  return (await http.post<TesteFcmResultado>('/integracoes/credenciais/fcm/testar')).data;
}

export function useTestarFcm() {
  return useMutation({ mutationFn: testarFcm });
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

// ---- WhatsApp (via Automais.Zap) ----

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

// ---- ElevenLabs (fala-para-texto do Agente IA) ----

export function useElevenLabs() {
  return useQuery({
    queryKey: keys.elevenlabs,
    queryFn: async () => (await http.get<ElevenLabs>('/integracoes/elevenlabs')).data,
  });
}

export function useSalvarElevenLabs() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarElevenLabs) => http.put('/integracoes/elevenlabs', payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.elevenlabs }),
  });
}

// Vozes da conta ElevenLabs (para o seletor). Vem vazio se a chave não estiver válida.
export function useElevenLabsVozes() {
  return useQuery({
    queryKey: ['integracoes', 'elevenlabs', 'vozes'] as const,
    queryFn: async () => (await http.get<VozElevenLabs[]>('/integracoes/elevenlabs/vozes')).data,
    staleTime: 60_000,
  });
}

// Busca vozes pt-BR na biblioteca pública (filtros opcionais de gênero/idade/texto).
export function useVozesBiblioteca(filtro: FiltroVozBiblioteca, habilitado: boolean) {
  return useQuery({
    queryKey: ['integracoes', 'elevenlabs', 'biblioteca', filtro] as const,
    queryFn: async () =>
      (
        await http.get<VozBibliotecaElevenLabs[]>('/integracoes/elevenlabs/biblioteca', {
          params: filtro,
        })
      ).data,
    enabled: habilitado,
  });
}

// Adiciona uma voz da biblioteca à conta e passa a usá-la.
export function useUsarVozBiblioteca() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { publicOwnerId: string; vozId: string; nome: string }) =>
      http.post('/integracoes/elevenlabs/biblioteca/usar', v),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: keys.elevenlabs });
      qc.invalidateQueries({ queryKey: ['integracoes', 'elevenlabs', 'vozes'] });
    },
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
