import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import type {
  AtualizarFonteDadosPayload,
  CriarFonteDadosPayload,
  EnderecoSisreg,
  FonteDados,
  ResultadoTesteFonte,
  TestarFonteDadosPayload,
  TokenAgente,
} from '@/features/fontes-dados/types';

export const fontesDadosKeys = {
  lista: ['fontes-dados'] as const,
  enderecoSisreg: ['fontes-dados', 'sisreg', 'endereco'] as const,
};

// As telas dos recursos (IA, Importar Prontuários, SER/SERNIT/ESUS SG, SISREG) mostram a fonte que
// usam: depois de mexer aqui, as listas delas também ficam velhas.
const LISTAS_DOS_RECURSOS = [['ia'], ['pep'], ['ser'], ['sernit'], ['esussg'], ['sisreg'], ['integracoes']];

function useInvalidar() {
  const client = useQueryClient();
  return () => {
    client.invalidateQueries({ queryKey: fontesDadosKeys.lista });
    for (const k of LISTAS_DOS_RECURSOS) client.invalidateQueries({ queryKey: k });
  };
}

export function useFontesDados() {
  return useQuery({
    queryKey: fontesDadosKeys.lista,
    queryFn: async () => (await http.get<FonteDados[]>('/fontes-dados')).data,
  });
}

export function useCriarFonteDados() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: async (payload: CriarFonteDadosPayload) =>
      (await http.post<string>('/fontes-dados', payload)).data,
    onSuccess: invalidar,
  });
}

export function useAtualizarFonteDados() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: async ({ chave, payload }: { chave: string; payload: AtualizarFonteDadosPayload }) => {
      await http.put(`/fontes-dados/${encodeURIComponent(chave)}`, payload);
    },
    onSuccess: invalidar,
  });
}

export function useRemoverFonteDados() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: async (chave: string) => {
      await http.delete(`/fontes-dados/${encodeURIComponent(chave)}`);
    },
    onSuccess: invalidar,
  });
}

export function useTestarFonteDados() {
  return useMutation({
    mutationFn: async ({ chave, payload }: { chave: string; payload?: TestarFonteDadosPayload }) =>
      (await http.post<ResultadoTesteFonte>(`/fontes-dados/${encodeURIComponent(chave)}/testar`, payload ?? {})).data,
  });
}

export function useGerarTokenAgente() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: async (id: string) => (await http.post<TokenAgente>(`/fontes-dados/${id}/token-agente`)).data,
    onSuccess: invalidar,
  });
}

export function useProvisionarAgente() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: async (payload: { slug: string; nome?: string }) =>
      (await http.post<TokenAgente>('/fontes-dados/agentes/provisionar', payload)).data,
    onSuccess: invalidar,
  });
}

// ---- Endereço do SISREG (IP atual, desde quando, túnel, histórico) ----

// O servidor verifica a cada 2 min; o bloco relê a cada 1 min enquanto está aberto.
export function useEnderecoSisreg() {
  return useQuery({
    queryKey: fontesDadosKeys.enderecoSisreg,
    queryFn: async () => (await http.get<EnderecoSisreg>('/integracoes/sisreg/endereco')).data,
    refetchInterval: 60_000,
  });
}
