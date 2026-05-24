import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarTemplate,
  cadastrarTemplate,
  desativarTemplate,
  listarTemplates,
  obterTemplate,
  reativarTemplate,
} from '@/features/laudo-templates/api/laudoTemplatesApi';
import type { SalvarLaudoTemplatePayload } from '@/features/laudo-templates/types';

export const laudoTemplatesKeys = {
  raiz: ['laudo-templates'] as const,
  lista: (categoria?: string, incluirInativos?: boolean) =>
    ['laudo-templates', 'lista', { categoria: categoria ?? null, incluirInativos: !!incluirInativos }] as const,
  porId: (id: string) => ['laudo-templates', 'detalhe', id] as const,
};

export function useListarTemplates(categoria?: string, incluirInativos = false) {
  return useQuery({
    queryKey: laudoTemplatesKeys.lista(categoria, incluirInativos),
    queryFn: () => listarTemplates(categoria, incluirInativos),
  });
}

export function useTemplatePorId(id: string | null) {
  return useQuery({
    queryKey: id ? laudoTemplatesKeys.porId(id) : ['laudo-templates', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterTemplate(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarTemplate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarLaudoTemplatePayload) => cadastrarTemplate(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: laudoTemplatesKeys.raiz }),
  });
}

export function useAtualizarTemplate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarLaudoTemplatePayload }) =>
      atualizarTemplate(id, payload),
    onSuccess: (_d, vars) => {
      client.invalidateQueries({ queryKey: laudoTemplatesKeys.raiz });
      client.invalidateQueries({ queryKey: laudoTemplatesKeys.porId(vars.id) });
    },
  });
}

export function useDesativarTemplate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarTemplate(id),
    onSuccess: () => client.invalidateQueries({ queryKey: laudoTemplatesKeys.raiz }),
  });
}

export function useReativarTemplate() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reativarTemplate(id),
    onSuccess: () => client.invalidateQueries({ queryKey: laudoTemplatesKeys.raiz }),
  });
}
