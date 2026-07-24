import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  apurarAba,
  apurarIndicador,
  atualizarIndicador,
  criarIndicador,
  excluirIndicador,
  listarFontes,
  listarIndicadores,
  listarUnidades,
  listarVersoes,
  obterIndicador,
} from '@/features/indicadores/api/indicadoresApi';
import type {
  AbaIndicador,
  FiltroIndicador,
  SalvarIndicadorPayload,
} from '@/features/indicadores/types';

export const indicadoresKeys = {
  raiz: ['indicadores'] as const,
  unidades: ['indicadores', 'unidades'] as const,
  fontes: ['indicadores', 'fontes'] as const,
  lista: (aba: AbaIndicador, filtro: FiltroIndicador) =>
    ['indicadores', 'lista', aba, filtro.hospital, filtro.inicio, filtro.fim] as const,
  detalhe: (id: string) => ['indicadores', 'detalhe', id] as const,
  versoes: (id: string) => ['indicadores', 'versoes', id] as const,
};

export function useUnidadesIndicador() {
  return useQuery({
    queryKey: indicadoresKeys.unidades,
    queryFn: listarUnidades,
    staleTime: 60 * 60 * 1000, // lista fixa (só o HMCML hoje)
  });
}

export function useListarIndicadores(aba: AbaIndicador, filtro: FiltroIndicador) {
  return useQuery({
    queryKey: indicadoresKeys.lista(aba, filtro),
    queryFn: () => listarIndicadores(aba, filtro),
  });
}

export function useIndicador(id: string | undefined) {
  return useQuery({
    queryKey: indicadoresKeys.detalhe(id ?? ''),
    queryFn: () => obterIndicador(id!),
    enabled: !!id,
  });
}

export function useVersoesIndicador(id: string | undefined) {
  return useQuery({
    queryKey: indicadoresKeys.versoes(id ?? ''),
    queryFn: () => listarVersoes(id!),
    enabled: !!id,
  });
}

/** Apura a aba inteira. Sem retry: é consulta pesada no Oracle de produção do hospital. */
export function useApurarAba(aba: AbaIndicador) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (filtro: FiltroIndicador) => apurarAba(aba, filtro),
    retry: false,
    onSuccess: () => client.invalidateQueries({ queryKey: indicadoresKeys.raiz }),
  });
}

export function useApurarIndicador() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      filtro,
      previa,
    }: {
      id: string;
      filtro: FiltroIndicador;
      previa?: boolean;
    }) => apurarIndicador(id, filtro, previa),
    retry: false,
    onSuccess: (_dados, variaveis) => {
      if (!variaveis.previa) client.invalidateQueries({ queryKey: indicadoresKeys.raiz });
    },
  });
}

export function useFontesIndicador() {
  return useQuery({
    queryKey: indicadoresKeys.fontes,
    queryFn: listarFontes,
    staleTime: 60 * 60 * 1000,
  });
}

export function useCriarIndicador() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarIndicadorPayload) => criarIndicador(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: indicadoresKeys.raiz }),
  });
}

export function useAtualizarIndicador() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarIndicadorPayload }) =>
      atualizarIndicador(id, payload),
    onSuccess: (_dados, variaveis) => {
      client.invalidateQueries({ queryKey: indicadoresKeys.raiz });
      client.invalidateQueries({ queryKey: indicadoresKeys.versoes(variaveis.id) });
    },
  });
}

export function useExcluirIndicador() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirIndicador(id),
    onSuccess: () => client.invalidateQueries({ queryKey: indicadoresKeys.raiz }),
  });
}
