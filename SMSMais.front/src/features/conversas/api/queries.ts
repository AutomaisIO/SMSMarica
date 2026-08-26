import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  assumirConversa,
  buscarContatos,
  devolverConversa,
  encaminharConversa,
  encaminharConversaParaRobo,
  enviarMensagem,
  iniciarConversa,
  listarAtendentesElegiveis,
  listarConversas,
  listarPacientesDoTelefone,
  listarTemplates,
  listarUnidadesDestino,
  marcarLida,
  obterConversa,
  obterMensagens,
  obterResumoConversas,
  transferirConversa,
} from '@/features/conversas/api/conversasApi';
import type { AbaConversas, IniciarConversaPayload } from '@/features/conversas/types';

export const conversasKeys = {
  raiz: ['conversas'] as const,
  lista: (aba: AbaConversas, busca?: string) => ['conversas', 'lista', aba, busca ?? ''] as const,
  detalhe: (id: string) => ['conversas', 'detalhe', id] as const,
  mensagens: (id: string) => ['conversas', 'mensagens', id] as const,
  templates: () => ['conversas', 'templates'] as const,
  contatos: (termo: string) => ['conversas', 'contatos', termo] as const,
  pacientesDoTelefone: (id: string) => ['conversas', 'pacientes-telefone', id] as const,
  atendentesElegiveis: (id: string) => ['conversas', 'atendentes-elegiveis', id] as const,
  unidadesDestino: () => ['conversas', 'unidades-destino'] as const,
  resumo: () => ['conversas', 'resumo'] as const,
};

/** Todos os cadastros que têm o telefone da conversa (celular de família). */
export function usePacientesDoTelefone(conversaId: string | null) {
  return useQuery({
    queryKey: conversasKeys.pacientesDoTelefone(conversaId ?? 'nenhum'),
    queryFn: () => listarPacientesDoTelefone(conversaId!),
    enabled: Boolean(conversaId),
    staleTime: 60_000,
  });
}

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
      // Responder zera o badge e, se a conversa estava sem dono, gera o claim (posse muda).
      invalidarPosse(client, v.id);
    },
  });
}

export function useMarcarLida() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => marcarLida(id),
    onSuccess: (_d, id) => invalidarPosse(client, id),
  });
}

/**
 * Invalidações comuns às ações de posse: a lista muda de aba, o cabeçalho da thread muda de
 * chip e o resumo do sino muda de conta. Também usada no onError — um 409 de corrida
 * ("já assumida por Fulano") significa que a tela está atrasada: refetch para mostrar o real.
 */
function invalidarPosse(client: ReturnType<typeof useQueryClient>, id?: string) {
  client.invalidateQueries({ queryKey: ['conversas', 'lista'] });
  client.invalidateQueries({ queryKey: conversasKeys.resumo() });
  if (id) {
    client.invalidateQueries({ queryKey: conversasKeys.detalhe(id) });
    client.invalidateQueries({ queryKey: conversasKeys.atendentesElegiveis(id) });
  }
}

export function useAssumirConversa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => assumirConversa(id),
    onSuccess: (_d, id) => invalidarPosse(client, id),
    onError: (_e, id) => invalidarPosse(client, id),
  });
}

export function useDevolverConversa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => devolverConversa(id),
    onSuccess: (_d, id) => invalidarPosse(client, id),
    onError: (_e, id) => invalidarPosse(client, id),
  });
}

export function useEncaminharConversa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, paraUsuarioId, observacao }: { id: string; paraUsuarioId: string; observacao?: string }) =>
      encaminharConversa(id, { paraUsuarioId, observacao: observacao || null }),
    onSuccess: (_d, v) => invalidarPosse(client, v.id),
    onError: (_e, v) => invalidarPosse(client, v.id),
  });
}

export function useEncaminharConversaParaRobo() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => encaminharConversaParaRobo(id),
    onSuccess: (_d, id) => invalidarPosse(client, id),
    onError: (_e, id) => invalidarPosse(client, id),
  });
}

export function useTransferirConversa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, paraUnidadeId, observacao }: { id: string; paraUnidadeId: string; observacao?: string }) =>
      transferirConversa(id, { paraUnidadeId, observacao: observacao || null }),
    onSuccess: (_d, v) => invalidarPosse(client, v.id),
    onError: (_e, v) => invalidarPosse(client, v.id),
  });
}

export function useAtendentesElegiveis(conversaId: string | null) {
  return useQuery({
    queryKey: conversasKeys.atendentesElegiveis(conversaId ?? 'nenhum'),
    queryFn: () => listarAtendentesElegiveis(conversaId!),
    enabled: Boolean(conversaId),
    staleTime: 60_000,
  });
}

export function useUnidadesDestino(habilitado: boolean) {
  return useQuery({
    queryKey: conversasKeys.unidadesDestino(),
    queryFn: listarUnidadesDestino,
    enabled: habilitado,
    staleTime: 5 * 60_000,
  });
}

/** Contadores do sino/badge. O SignalR invalida em tempo real; o poll cobre socket caído. */
export function useResumoConversas(habilitado: boolean) {
  return useQuery({
    queryKey: conversasKeys.resumo(),
    queryFn: obterResumoConversas,
    enabled: habilitado,
    refetchInterval: 30_000,
  });
}
