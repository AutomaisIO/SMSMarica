import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarRota,
  cadastrarRota,
  cancelarRota,
  concluirRota,
  criarAlocacao,
  gerarTranslado,
  iniciarRota,
  listarRotas,
  listarSessoesElegiveis,
  obterRotaPorId,
  removerAlocacao,
} from '@/features/translados/api/transladosApi';
import type {
  AtualizarRotaPayload,
  CadastrarRotaPayload,
  CriarAlocacaoPayload,
  FiltrosListarRotas,
  GerarTransladoPayload,
} from '@/features/translados/types';

export const transladosKeys = {
  raiz: ['rotas'] as const,
  lista: (f: FiltrosListarRotas = {}) => ['rotas', 'lista', f.data ?? null, f.motoristaId ?? null, f.veiculoId ?? null] as const,
  detalhe: (id: string) => ['rotas', 'detalhe', id] as const,
  sessoesElegiveis: (rotaId: string) => ['rotas', 'sessoes-elegiveis', rotaId] as const,
};

export function useListarRotas(filtros: FiltrosListarRotas = {}) {
  return useQuery({
    queryKey: transladosKeys.lista(filtros),
    queryFn: () => listarRotas(filtros),
  });
}

export function useRotaPorId(id: string | null) {
  return useQuery({
    queryKey: id ? transladosKeys.detalhe(id) : ['rotas', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterRotaPorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useSessoesElegiveis(rotaId: string | null) {
  return useQuery({
    queryKey: rotaId ? transladosKeys.sessoesElegiveis(rotaId) : ['rotas', 'sessoes-elegiveis', 'nenhum'],
    queryFn: () => {
      if (!rotaId) throw new Error('ID não informado.');
      return listarSessoesElegiveis(rotaId);
    },
    enabled: Boolean(rotaId),
  });
}

export function useGerarTranslado() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: GerarTransladoPayload) => gerarTranslado(payload),
    onSuccess: (resultado) => {
      // Só invalida a lista quando realmente gravou (confirmar=true).
      if (resultado.confirmado) {
        client.invalidateQueries({ queryKey: transladosKeys.raiz });
      }
    },
  });
}

export function useCadastrarRota() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarRotaPayload) => cadastrarRota(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: transladosKeys.raiz }),
  });
}

export function useAtualizarRota() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarRotaPayload }) =>
      atualizarRota(id, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: transladosKeys.raiz });
      client.invalidateQueries({ queryKey: transladosKeys.detalhe(v.id) });
    },
  });
}

export function useIniciarRota() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => iniciarRota(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: transladosKeys.raiz });
      client.invalidateQueries({ queryKey: transladosKeys.detalhe(id) });
    },
  });
}

export function useConcluirRota() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => concluirRota(id),
    onSuccess: (_d, id) => {
      client.invalidateQueries({ queryKey: transladosKeys.raiz });
      client.invalidateQueries({ queryKey: transladosKeys.detalhe(id) });
    },
  });
}

export function useCancelarRota() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelarRota(id),
    onSuccess: () => client.invalidateQueries({ queryKey: transladosKeys.raiz }),
  });
}

export function useCriarAlocacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ rotaId, payload }: { rotaId: string; payload: CriarAlocacaoPayload }) =>
      criarAlocacao(rotaId, payload),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: transladosKeys.detalhe(v.rotaId) });
      client.invalidateQueries({ queryKey: transladosKeys.sessoesElegiveis(v.rotaId) });
      client.invalidateQueries({ queryKey: transladosKeys.raiz });
      client.invalidateQueries({ queryKey: ['tratamentos'] });
    },
  });
}

export function useRemoverAlocacao() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ rotaId, alocacaoId }: { rotaId: string; alocacaoId: string }) =>
      removerAlocacao(rotaId, alocacaoId),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: transladosKeys.detalhe(v.rotaId) });
      client.invalidateQueries({ queryKey: transladosKeys.sessoesElegiveis(v.rotaId) });
      client.invalidateQueries({ queryKey: transladosKeys.raiz });
      client.invalidateQueries({ queryKey: ['tratamentos'] });
    },
  });
}
