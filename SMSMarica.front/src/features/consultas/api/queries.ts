import { useQuery } from '@tanstack/react-query';
import { listarConsultas, obterConsulta } from '@/features/consultas/api/consultasApi';
import type { FiltroConsultas } from '@/features/consultas/types';

export const consultasKeys = {
  raiz: ['consultas'] as const,
  lista: (filtro: FiltroConsultas) => ['consultas', 'lista', filtro] as const,
  detalhe: (id: string) => ['consultas', 'detalhe', id] as const,
};

export function useListarConsultas(filtro: FiltroConsultas) {
  return useQuery({
    queryKey: consultasKeys.lista(filtro),
    queryFn: () => listarConsultas(filtro),
  });
}

export function useObterConsulta(id: string | null) {
  return useQuery({
    queryKey: consultasKeys.detalhe(id ?? ''),
    queryFn: () => obterConsulta(id!),
    enabled: !!id,
  });
}
