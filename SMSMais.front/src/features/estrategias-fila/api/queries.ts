import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  arquivarEstrategia,
  atualizarEstrategia,
  criarEstrategia,
  excluirEstrategia,
  listarEstrategias,
  listarProcedimentos,
  marcarAplicada,
  obterCenario,
  obterEstrategia,
  obterRodada,
  rodar,
  simular,
} from '@/features/estrategias-fila/api/estrategiasApi';
import type { ModoRodada, ParametrosEstrategia, StatusEstrategia } from '@/features/estrategias-fila/types';

export const estrategiasKeys = {
  raiz: ['estrategias-fila'] as const,
  procedimentos: (busca: string, ordenar: string) => ['estrategias-fila', 'procedimentos', busca, ordenar] as const,
  cenario: (codigo: string | null, nome: string) => ['estrategias-fila', 'cenario', codigo ?? '', nome] as const,
  lista: (incluirArquivadas: boolean, status: StatusEstrategia | null) =>
    ['estrategias-fila', 'lista', incluirArquivadas, status ?? ''] as const,
  uma: (id: string) => ['estrategias-fila', 'uma', id] as const,
  rodada: (id: string, numero: number) => ['estrategias-fila', 'rodada', id, numero] as const,
};

export function useProcedimentosComFila(busca: string, ordenar: string) {
  // O backend já cacheia a lista por 10 min; aqui só se evita refazer a chamada a cada tecla.
  return useQuery({
    queryKey: estrategiasKeys.procedimentos(busca, ordenar),
    queryFn: () => listarProcedimentos(busca, ordenar),
    staleTime: 60_000,
    placeholderData: (anterior) => anterior,
  });
}

export function useCenario(codigo: string | null, nome: string | null) {
  return useQuery({
    queryKey: estrategiasKeys.cenario(codigo, nome ?? ''),
    queryFn: () => obterCenario(codigo, nome!),
    enabled: !!nome,
    staleTime: 5 * 60_000,
  });
}

export function useSimular() {
  return useMutation({
    mutationFn: ({ codigo, nome, parametros }: { codigo: string | null; nome: string; parametros: ParametrosEstrategia }) =>
      simular(codigo, nome, parametros),
  });
}

export function useEstrategias(incluirArquivadas = false, status: StatusEstrategia | null = null) {
  return useQuery({
    queryKey: estrategiasKeys.lista(incluirArquivadas, status),
    queryFn: () => listarEstrategias({ incluirArquivadas, status }),
  });
}

export function useEstrategia(id: string | undefined) {
  return useQuery({
    queryKey: estrategiasKeys.uma(id ?? ''),
    queryFn: () => obterEstrategia(id!),
    enabled: !!id,
  });
}

export function useRodada(id: string | undefined, numero: number | null) {
  return useQuery({
    queryKey: estrategiasKeys.rodada(id ?? '', numero ?? 0),
    queryFn: () => obterRodada(id!, numero!),
    enabled: !!id && numero !== null,
  });
}

export function useCriarEstrategia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: criarEstrategia,
    onSuccess: () => client.invalidateQueries({ queryKey: estrategiasKeys.raiz }),
  });
}

export function useAtualizarEstrategia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...payload }: { id: string; nome: string; parametros: ParametrosEstrategia; status?: StatusEstrategia | null }) =>
      atualizarEstrategia(id, payload),
    onSuccess: (_, v) => client.invalidateQueries({ queryKey: estrategiasKeys.uma(v.id) }),
  });
}

export function useRodar() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, modo, parametros }: { id: string; modo: ModoRodada; parametros: ParametrosEstrategia }) =>
      rodar(id, modo, parametros),
    onSuccess: (_, v) => {
      client.invalidateQueries({ queryKey: estrategiasKeys.uma(v.id) });
      client.invalidateQueries({ queryKey: estrategiasKeys.lista(false, null) });
    },
  });
}

export function useMarcarAplicada() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nota }: { id: string; nota: string | null }) => marcarAplicada(id, nota),
    onSuccess: () => client.invalidateQueries({ queryKey: estrategiasKeys.raiz }),
  });
}

export function useArquivarEstrategia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: arquivarEstrategia,
    onSuccess: () => client.invalidateQueries({ queryKey: estrategiasKeys.raiz }),
  });
}

export function useExcluirEstrategia() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: excluirEstrategia,
    onSuccess: () => client.invalidateQueries({ queryKey: estrategiasKeys.raiz }),
  });
}
