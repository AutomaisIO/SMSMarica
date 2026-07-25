import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  alternarProcedimento,
  alternarProfissional,
  alternarProfissionaisEmLote,
  atualizarMapeamento,
  obterCredencial,
  obterMapeamento,
  removerCredencial,
  salvarCredencial,
  sincronizarFhir,
  testarCredencial,
} from '@/features/sisreg-mapeamento/api/mapeamentoApi';
import type { SalvarCredencialPayload } from '@/features/sisreg-mapeamento/types';

export const mapeamentoKeys = {
  /** A unidade entra na chave: trocar de unidade tem que trocar de mapeamento. */
  mapeamento: (unidadeId: string | null) => ['sisreg-mapeamento', unidadeId] as const,
  credencial: (unidadeId: string | null) => ['sisreg-mapeamento', 'credencial', unidadeId] as const,
};

export function useMapeamento(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.mapeamento(unidadeId),
    queryFn: obterMapeamento,
    enabled: Boolean(unidadeId),
  });
}

export function useCredencialUnidade(unidadeId: string | null) {
  return useQuery({
    queryKey: mapeamentoKeys.credencial(unidadeId),
    queryFn: obterCredencial,
    enabled: Boolean(unidadeId),
  });
}

export function useAtualizarMapeamento(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: atualizarMapeamento,
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useAlternarProfissional(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, habilitado }: { id: string; habilitado: boolean }) =>
      alternarProfissional(id, habilitado),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useAlternarProcedimento(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, habilitado }: { id: string; habilitado: boolean }) =>
      alternarProcedimento(id, habilitado),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useAlternarProfissionaisEmLote(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ ids, habilitado }: { ids: string[]; habilitado: boolean }) =>
      alternarProfissionaisEmLote(ids, habilitado),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useSincronizarFhir(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: sincronizarFhir,
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.mapeamento(unidadeId) }),
  });
}

export function useSalvarCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarCredencialPayload) => salvarCredencial(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}

export function useTestarCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: testarCredencial,
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}

export function useRemoverCredencial(unidadeId: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: removerCredencial,
    onSuccess: () => client.invalidateQueries({ queryKey: mapeamentoKeys.credencial(unidadeId) }),
  });
}
