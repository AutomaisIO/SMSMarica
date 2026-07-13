import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarContatos,
  enviarMensagem,
  iniciarConversa,
  listarConversas,
  listarTemplates,
  marcarLida,
  obterConversa,
  obterMensagens,
} from '@/features/conversas/api/conversasApi';
import type { AbaConversas, IniciarConversaPayload } from '@/features/conversas/types';

export const conversasKeys = {
  raiz: ['conversas'] as const,
  lista: (aba: AbaConversas, busca?: string) => ['conversas', 'lista', aba, busca ?? ''] as const,
  detalhe: (id: string) => ['conversas', 'detalhe', id] as const,
  mensagens: (id: string) => ['conversas', 'mensagens', id] as const,
  templates: () => ['conversas', 'templates'] as const,
  contatos: (termo: string) => ['conversas', 'contatos', termo] as const,
};

/** Busca do destinatário na nova conversa: nº da solicitação, CPF, CNS ou parte do nome. */
export function useBuscarContatos(termo: string) {
  const limpo = termo.trim();
  return useQuery({
    queryKey: conversasKeys.contatos(limpo),
    queryFn: () => buscarContatos(limpo),
    enabled: limpo.length >= 3,
    staleTime: 30_000,
  });
}

export function useListarConversas(aba: AbaConversas, busca?: string) {
  return useQuery({
    queryKey: conversasKeys.lista(aba, busca),
    queryFn: () => listarConversas(aba, busca),
    // Fallback: o SignalR invalida em tempo real; o poll cobre reconexão/queda do socket.
    refetchInterval: 30_000,
  });
}

export function useConversa(id: string | null) {
  return useQuery({
    queryKey: id ? conversasKeys.detalhe(id) : ['conversas', 'detalhe', 'nenhum'],
    queryFn: () => obterConversa(id!),
    enabled: Boolean(id),
  });
}

export function useMensagens(id: string | null) {
  return useQuery({
    queryKey: id ? conversasKeys.mensagens(id) : ['conversas', 'mensagens', 'nenhum'],
    queryFn: () => obterMensagens(id!),
    enabled: Boolean(id),
    // Fallback: o SignalR invalida em tempo real; o poll cobre reconexão/queda do socket.
    refetchInterval: 30_000,
  });
}

export function useTemplates(habilitado: boolean) {
  return useQuery({
    queryKey: conversasKeys.templates(),
    queryFn: listarTemplates,
    enabled: habilitado,
    staleTime: 5 * 60_000,
  });
}

export function useIniciarConversa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: IniciarConversaPayload) => iniciarConversa(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: conversasKeys.raiz }),
  });
}

export function useEnviarMensagem() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, texto }: { id: string; texto: string }) => enviarMensagem(id, texto),
    onSuccess: (_d, v) => {
      client.invalidateQueries({ queryKey: conversasKeys.mensagens(v.id) });
      client.invalidateQueries({ queryKey: conversasKeys.detalhe(v.id) });
      client.invalidateQueries({ queryKey: ['conversas', 'lista'] });
    },
  });
}

export function useMarcarLida() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarLida(id),
    onSuccess: () => client.invalidateQueries({ queryKey: ['conversas', 'lista'] }),
  });
}
