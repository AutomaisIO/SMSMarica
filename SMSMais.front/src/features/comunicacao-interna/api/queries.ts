import { useEffect } from 'react';
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
  type QueryClient,
} from '@tanstack/react-query';
import * as api from '@/features/comunicacao-interna/api/comunicacaoInternaApi';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import type {
  ConversaInternaDetalhe,
  ConversaInternaItem,
  MensagemInterna,
  PaginaMensagensInternas,
} from '@/features/comunicacao-interna/types';

export const chavesInterno = {
  tudo: ['interno'] as const,
  contexto: ['interno', 'contexto'] as const,
  resumo: ['interno', 'resumo'] as const,
  conversas: ['interno', 'conversas'] as const,
  detalhe: (id: string) => ['interno', 'detalhe', id] as const,
  mensagens: (id: string) => ['interno', 'mensagens', id] as const,
  unidades: ['interno', 'unidades'] as const,
  usuarios: (unidadeId: string) => ['interno', 'usuarios', unidadeId] as const,
  busca: (q: string) => ['interno', 'busca', q] as const,
};

/** Quem sou eu no chat interno (id, minhas unidades, unidade atual). Muda pouco. */
export function useContextoInterno(habilitado = true) {
  return useQuery({
    queryKey: chavesInterno.contexto,
    queryFn: api.obterContexto,
    enabled: habilitado,
    staleTime: 5 * 60_000,
  });
}

/**
 * Total de não-lidas (balão laranja, título da aba). O hub invalida a cada evento; o intervalo é
 * só a rede de segurança para socket caído.
 */
export function useResumoInterno(habilitado: boolean) {
  const q = useQuery({
    queryKey: chavesInterno.resumo,
    queryFn: api.obterResumo,
    enabled: habilitado,
    refetchInterval: 60_000,
  });
  const setTotal = useInterno((s) => s.setTotalNaoLidas);
  useEffect(() => {
    if (q.data) setTotal(q.data.naoLidas);
  }, [q.data, setTotal]);
  return q;
}

export function useConversasInternas(habilitado = true) {
  return useQuery({
    queryKey: chavesInterno.conversas,
    queryFn: api.listarConversas,
    enabled: habilitado,
    refetchInterval: 60_000,
  });
}

export function useConversaInterna(id: string | null) {
  return useQuery({
    queryKey: chavesInterno.detalhe(id ?? ''),
    queryFn: () => api.obterConversa(id!),
    enabled: Boolean(id),
  });
}

/**
 * Mensagens em páginas: a página 0 é a MAIS RECENTE; as seguintes vêm para trás (`antesDe` = a
 * mais antiga da página anterior). Cada página já vem em ordem cronológica.
 */
export function useMensagensInternas(conversaId: string | null) {
  return useInfiniteQuery({
    queryKey: chavesInterno.mensagens(conversaId ?? ''),
    queryFn: ({ pageParam }) => api.listarMensagens(conversaId!, pageParam),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (ultima) => (ultima.temMais ? ultima.mensagens[0]?.enviadaEm : undefined),
    enabled: Boolean(conversaId),
    // A thread é alimentada pelo hub (anexarMensagemNoCache); refazer todas as páginas a cada foco
    // seria caro e desnecessário. Reconexão do hub invalida tudo.
    refetchOnWindowFocus: false,
    staleTime: Infinity,
  });
}

/**
 * Põe a mensagem no fim da página mais recente da thread, se ela estiver em cache. Idempotente
 * pelo id — o eco do hub e a resposta do POST da mesma mensagem não duplicam.
 */
export function anexarMensagemNoCache(qc: QueryClient, m: MensagemInterna) {
  qc.setQueryData<InfiniteData<PaginaMensagensInternas, string | undefined>>(
    chavesInterno.mensagens(m.conversaId),
    (velho) => {
      if (!velho || velho.pages.length === 0) return velho;
      if (velho.pages.some((p) => p.mensagens.some((x) => x.id === m.id))) return velho;
      const [primeira, ...resto] = velho.pages;
      return { ...velho, pages: [{ ...primeira, mensagens: [...primeira.mensagens, m] }, ...resto] };
    },
  );
}

/** Põe a transcrição no áudio que já está na tela (resposta do POST ou evento do hub). */
export function transcricaoNoCache(qc: QueryClient, conversaId: string, mensagemId: string, transcricao: string) {
  qc.setQueryData<InfiniteData<PaginaMensagensInternas, string | undefined>>(chavesInterno.mensagens(conversaId), (velho) =>
    velho
      ? {
          ...velho,
          pages: velho.pages.map((p) => ({
            ...p,
            mensagens: p.mensagens.map((x) =>
              x.id === mensagemId && x.anexo ? { ...x, anexo: { ...x.anexo, transcricao } } : x,
            ),
          })),
        }
      : velho,
  );
}

export function useTranscreverMensagemInterna() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.transcreverMensagem,
    onSuccess: (m) => {
      if (m.anexo?.transcricao) transcricaoNoCache(qc, m.conversaId, m.id, m.anexo.transcricao);
    },
  });
}

/** Atualiza a prévia da conversa na lista sem esperar o refetch (a ordem acerta no refetch). */
function atualizarPreviaNaLista(qc: QueryClient, m: MensagemInterna) {
  qc.setQueryData<ConversaInternaItem[]>(chavesInterno.conversas, (lista) =>
    lista?.map((c) =>
      c.id === m.conversaId
        ? {
            ...c,
            ultimaMensagemEm: m.enviadaEm,
            ultimaMensagemPreview: m.texto ?? (m.anexo ? (m.anexo.ehAudio ? '🎤 Áudio' : `📎 ${m.anexo.nome}`) : null),
            ultimaMensagemAutorNome: m.autorNome,
          }
        : c,
    ),
  );
}

export function useEnviarMensagemInterna() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.enviarMensagem,
    onSuccess: (m) => {
      anexarMensagemNoCache(qc, m);
      atualizarPreviaNaLista(qc, m);
      void qc.invalidateQueries({ queryKey: chavesInterno.conversas });
    },
  });
}

export function useMarcarLidaInterna() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.marcarLida,
    onSuccess: (_, conversaId) => {
      qc.setQueryData<ConversaInternaItem[]>(chavesInterno.conversas, (lista) =>
        lista?.map((c) => (c.id === conversaId ? { ...c, naoLidas: 0 } : c)),
      );
      void qc.invalidateQueries({ queryKey: chavesInterno.resumo });
    },
  });
}

export function useSilenciarConversaInterna() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, silenciada }: { id: string; silenciada: boolean }) => api.silenciarConversa(id, silenciada),
    onSuccess: (_, { id, silenciada }) => {
      qc.setQueryData<ConversaInternaItem[]>(chavesInterno.conversas, (lista) =>
        lista?.map((c) => (c.id === id ? { ...c, silenciada } : c)),
      );
      qc.setQueryData<ConversaInternaDetalhe>(chavesInterno.detalhe(id), (d) =>
        d ? { ...d, conversa: { ...d.conversa, silenciada } } : d,
      );
      void qc.invalidateQueries({ queryKey: chavesInterno.resumo });
    },
  });
}

/** Abre (ou cria) a conversa e já põe na lista — devolve o item para a tela selecionar. */
export function useAbrirConversaInterna() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (
      alvo: { tipo: 'Unidade'; unidadeDestinoId: string; unidadeOrigemId: string | null } | { tipo: 'Direta'; usuarioId: string },
    ) =>
      alvo.tipo === 'Unidade'
        ? api.abrirConversaUnidade(alvo.unidadeDestinoId, alvo.unidadeOrigemId)
        : api.abrirConversaDireta(alvo.usuarioId),
    onSuccess: (item) => {
      qc.setQueryData<ConversaInternaItem[]>(chavesInterno.conversas, (lista) =>
        lista ? (lista.some((c) => c.id === item.id) ? lista : [item, ...lista]) : lista,
      );
      void qc.invalidateQueries({ queryKey: chavesInterno.conversas });
    },
  });
}

export function useUnidadesInternas(habilitado: boolean) {
  return useQuery({
    queryKey: chavesInterno.unidades,
    queryFn: api.listarUnidades,
    enabled: habilitado,
    staleTime: 15_000,
  });
}

export function useUsuariosDaUnidadeInterna(unidadeId: string | null) {
  return useQuery({
    queryKey: chavesInterno.usuarios(unidadeId ?? ''),
    queryFn: () => api.listarUsuariosDaUnidade(unidadeId!),
    enabled: Boolean(unidadeId),
    staleTime: 15_000,
  });
}

export function useBuscaInterna(q: string) {
  const termo = q.trim();
  return useQuery({
    queryKey: chavesInterno.busca(termo),
    queryFn: () => api.buscar(termo),
    enabled: termo.length >= 2,
    staleTime: 30_000,
  });
}
