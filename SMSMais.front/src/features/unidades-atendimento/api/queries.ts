import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarUnidadeAtendimento,
  cadastrarUnidadeAtendimento,
  desativarUnidadeAtendimento,
  listarUnidadesAtendimento,
  obterUnidadeAtendimento,
  reativarUnidadeAtendimento,
} from '@/features/unidades-atendimento/api/unidadesAtendimentoApi';
import type { SalvarUnidadeAtendimentoPayload } from '@/features/unidades-atendimento/types';

export const unidadesAtendimentoKeys = {
  raiz: ['unidades-atendimento'] as const,
  lista: (incluirInativas: boolean) => ['unidades-atendimento', 'lista', incluirInativas] as const,
  porId: (id: string) => ['unidades-atendimento', 'detalhe', id] as const,
};

/** As opções do seletor no tratamento vivem em outra chave (tratamentos) — invalidar as duas. */
function invalidarTudo(client: ReturnType<typeof useQueryClient>) {
  client.invalidateQueries({ queryKey: unidadesAtendimentoKeys.raiz });
  client.invalidateQueries({ queryKey: ['tratamentos', 'unidades-atendimento'] });
}

export function useListarUnidadesAtendimento(incluirInativas = false) {
  return useQuery({
    queryKey: unidadesAtendimentoKeys.lista(incluirInativas),
    queryFn: () => listarUnidadesAtendimento(incluirInativas),
  });
}

export function useUnidadeAtendimento(id: string | null) {
  return useQuery({
    queryKey: id ? unidadesAtendimentoKeys.porId(id) : ['unidades-atendimento', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterUnidadeAtendimento(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarUnidadeAtendimento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarUnidadeAtendimentoPayload) => cadastrarUnidadeAtendimento(payload),
    onSuccess: () => invalidarTudo(client),
  });
}

export function useAtualizarUnidadeAtendimento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarUnidadeAtendimentoPayload }) =>
      atualizarUnidadeAtendimento(id, payload),
    onSuccess: () => invalidarTudo(client),
  });
}

export function useDesativarUnidadeAtendimento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarUnidadeAtendimento(id),
    onSuccess: () => invalidarTudo(client),
  });
}

export function useReativarUnidadeAtendimento() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reativarUnidadeAtendimento(id),
    onSuccess: () => invalidarTudo(client),
  });
}
