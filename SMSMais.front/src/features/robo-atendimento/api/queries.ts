import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarAssunto,
  catalogoComandos,
  criarAssunto,
  excluirAssunto,
  listarAssuntos,
  listarErrosRobo,
  obterAssunto,
  obterConfiguracao,
  revisarErroRobo,
  salvarConfiguracao,
} from '@/features/robo-atendimento/api/roboApi';
import type {
  RoboConfiguracao,
  SalvarRoboAssuntoPayload,
  StatusRoboErro,
} from '@/features/robo-atendimento/types';

export const roboKeys = {
  raiz: ['robo-atendimento'] as const,
  lista: (incluirInativos?: boolean) =>
    ['robo-atendimento', 'assuntos', { incluirInativos: !!incluirInativos }] as const,
  assunto: (id: string) => ['robo-atendimento', 'assunto', id] as const,
  catalogo: ['robo-atendimento', 'catalogo-comandos'] as const,
  config: ['robo-atendimento', 'configuracao'] as const,
  erros: (status?: StatusRoboErro) => ['robo-atendimento', 'erros', status ?? 'todos'] as const,
};

export function useListarAssuntos(incluirInativos = false) {
  return useQuery({
    queryKey: roboKeys.lista(incluirInativos),
    queryFn: () => listarAssuntos(incluirInativos),
  });
}

export function useObterAssunto(id: string | null) {
  return useQuery({
    queryKey: roboKeys.assunto(id ?? ''),
    queryFn: () => obterAssunto(id as string),
    enabled: !!id,
  });
}

export function useCatalogoComandos() {
  return useQuery({ queryKey: roboKeys.catalogo, queryFn: catalogoComandos, staleTime: 1000 * 60 * 30 });
}

export function useCriarAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarRoboAssuntoPayload) => criarAssunto(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useAtualizarAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarRoboAssuntoPayload }) =>
      atualizarAssunto(id, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useExcluirAssunto() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirAssunto(id),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.raiz }),
  });
}

export function useConfiguracaoRobo() {
  return useQuery({ queryKey: roboKeys.config, queryFn: obterConfiguracao });
}

export function useSalvarConfiguracaoRobo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: RoboConfiguracao) => salvarConfiguracao(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: roboKeys.config }),
  });
}

export function useListarErrosRobo(status?: StatusRoboErro) {
  return useQuery({
    queryKey: roboKeys.erros(status),
    queryFn: () => listarErrosRobo(status),
  });
}

export function useRevisarErroRobo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, nota }: { id: string; status: 'Revisado' | 'Descartado'; nota?: string }) =>
      revisarErroRobo(id, { status, nota: nota || null }),
    onSuccess: () => client.invalidateQueries({ queryKey: ['robo-atendimento', 'erros'] }),
  });
}
