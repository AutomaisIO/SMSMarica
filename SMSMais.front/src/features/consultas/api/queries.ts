import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listarConsultas,
  obterConsulta,
  obterHistoricoConsulta,
  registrarContatoConsulta,
  reenviarComunicacaoConsulta,
} from '@/features/consultas/api/consultasApi';
import type { FiltroConsultas } from '@/features/consultas/types';

export const consultasKeys = {
  raiz: ['consultas'] as const,
  lista: (filtro: FiltroConsultas) => ['consultas', 'lista', filtro] as const,
  detalhe: (id: string) => ['consultas', 'detalhe', id] as const,
  historico: (id: string) => ['consultas', 'historico', id] as const,
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

export function useHistoricoConsulta(id: string | null) {
  return useQuery({
    queryKey: consultasKeys.historico(id ?? ''),
    queryFn: () => obterHistoricoConsulta(id!),
    enabled: !!id,
  });
}

export function useReenviarComunicacaoConsulta() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, comunicacaoId }: { id: string; comunicacaoId: string }) =>
      reenviarComunicacaoConsulta(id, comunicacaoId),
    onSuccess: (_r, { id }) => {
      client.invalidateQueries({ queryKey: consultasKeys.historico(id) });
      client.invalidateQueries({ queryKey: ['consultas', 'lista'] });
    },
  });
}

export function useRegistrarContatoConsulta() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      id,
      meio,
      resultado,
      observacao,
    }: {
      id: string;
      meio: string;
      resultado: string;
      observacao: string | null;
    }) => registrarContatoConsulta(id, { meio, resultado, observacao }),
    onSuccess: (_r, { id }) => client.invalidateQueries({ queryKey: consultasKeys.historico(id) }),
  });
}
