import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { http } from '@/shared/api/httpClient';
import { params } from '@/features/mensageria/api/comunicacoesApi';
import type {
  AcaoResultado,
  AgendamentoPendentePaciente,
  AtendenteConfirmacao,
  AtoAtendente,
  EquipeConfirmacoes,
  EventoAtendimento,
  FiltroAtendimento,
  MensagemContexto,
  MotivosTelefoneComprometido,
  PaginaAtendimento,
  ResumoAbas,
} from '@/features/confirmacoes/types';

const raiz = ['confirmacoes', 'atendimento'] as const;

export function useResumoAbas() {
  return useQuery({
    queryKey: [...raiz, 'resumo'],
    queryFn: async () => (await http.get<ResumoAbas>('/confirmacoes/atendimento/resumo')).data,
    refetchInterval: 30_000,
  });
}

export function useAtendimento(filtro: FiltroAtendimento) {
  return useQuery({
    queryKey: [...raiz, 'lista', filtro],
    queryFn: async () =>
      (await http.get<PaginaAtendimento>('/confirmacoes/atendimento', { params: params(filtro) })).data,
    placeholderData: (anterior) => anterior,
    refetchInterval: 30_000,
  });
}

/** Os porquês da aba Telefone comprometido — só busca quando a aba está aberta. */
export function useMotivosTelefoneComprometido(habilitado: boolean) {
  return useQuery({
    queryKey: [...raiz, 'telefone-comprometido', 'motivos'],
    queryFn: async () =>
      (await http.get<MotivosTelefoneComprometido>('/confirmacoes/atendimento/telefone-comprometido/motivos')).data,
    enabled: habilitado,
    refetchInterval: 60_000,
  });
}

/**
 * Contexto da conversa — só carrega quando a atendente abre, porque é leitura pesada e a maioria
 * dos cards não precisa dela.
 */
export function useConversaContexto(solicitacaoId: string | null) {
  return useQuery({
    queryKey: [...raiz, 'conversa', solicitacaoId],
    queryFn: async () =>
      (await http.get<MensagemContexto[]>(`/confirmacoes/atendimento/${solicitacaoId}/conversa`)).data,
    enabled: !!solicitacaoId,
    staleTime: 30_000,
  });
}

/** Estado da sessão de ESCRITA no SISREG (o cancelamento assina com o login do operador). */
export type SessaoSisreg = {
  autenticado: boolean;
  usuarioSisreg: string | null;
  autenticadaEm: string | null;
  expiraEm: string | null;
};

export function useSessaoSisreg() {
  return useQuery({
    queryKey: ['sisreg', 'sessao'],
    queryFn: async () => (await http.get<SessaoSisreg>('/sisreg/sessao')).data,
    staleTime: 60_000,
  });
}

export function useEntrarNoSisreg() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ usuario, senha }: { usuario: string; senha: string }) =>
      (await http.post<SessaoSisreg>('/sisreg/sessao', { usuario, senha })).data,
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['sisreg', 'sessao'] }),
  });
}

/**
 * Agendamentos do paciente que ainda esperam confirmação — para o botão "Confirmar" da janela de
 * chat (#133). Só busca quando há paciente e o chamador habilita (o botão só aparece com permissão).
 */
export function usePendentesDoPaciente(pacienteId: string | null, habilitado = true) {
  return useQuery({
    queryKey: [...raiz, 'paciente-pendentes', pacienteId],
    queryFn: async () =>
      (await http.get<AgendamentoPendentePaciente[]>(`/confirmacoes/atendimento/paciente/${pacienteId}/pendentes`)).data,
    enabled: habilitado && Boolean(pacienteId),
    staleTime: 15_000,
  });
}

export function useAtendentes() {
  return useQuery({
    queryKey: [...raiz, 'atendentes'],
    queryFn: async () => (await http.get<AtendenteConfirmacao[]>('/confirmacoes/atendimento/atendentes')).data,
    staleTime: 5 * 60_000,
  });
}

export function useHistoricoAtendimento(solicitacaoId: string | null) {
  return useQuery({
    queryKey: [...raiz, 'historico', solicitacaoId],
    queryFn: async () =>
      (await http.get<EventoAtendimento[]>(`/confirmacoes/atendimento/${solicitacaoId}/historico`)).data,
    enabled: Boolean(solicitacaoId),
  });
}

/** Aba Equipe: produção por atendente. Só é chamado por quem tem o módulo ConfirmacoesEquipe. */
export function useEquipeConfirmacoes(de: string, ate: string, habilitado: boolean) {
  return useQuery({
    queryKey: ['confirmacoes', 'equipe', de, ate],
    queryFn: async () => (await http.get<EquipeConfirmacoes>('/confirmacoes/equipe', { params: { de, ate } })).data,
    enabled: habilitado,
    placeholderData: (anterior) => anterior,
    refetchInterval: 60_000,
  });
}

export function useAtosDaAtendente(usuarioId: string | null, de: string, ate: string) {
  return useQuery({
    queryKey: ['confirmacoes', 'equipe', 'atos', usuarioId, de, ate],
    queryFn: async () =>
      (await http.get<AtoAtendente[]>(`/confirmacoes/equipe/${usuarioId}/atos`, { params: { de, ate } })).data,
    enabled: Boolean(usuarioId),
  });
}

type Acao =
  | { tipo: 'atender' | 'assumir' | 'liberar' }
  | { tipo: 'confirmar'; meio?: string; observacao?: string }
  | { tipo: 'cancelar'; motivo: string; meio?: string }
  | { tipo: 'pendente'; motivo: string }
  | { tipo: 'contato-errado'; observacao?: string }
  | { tipo: 'transferir'; paraUsuarioId: string; observacao?: string }
  | { tipo: 'contato-corrigido'; telefone?: string; observacao?: string }
  | { tipo: 'desfazer-pedido-cancelamento'; observacao?: string };

/** Uma mutação para todas as ações do card — invalida as listas ao terminar. */
export function useAcaoAtendimento() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ solicitacaoId, acao }: { solicitacaoId: string; acao: Acao }) => {
      const { tipo, ...corpo } = acao;
      const { data } = await http.post<AcaoResultado>(`/confirmacoes/atendimento/${solicitacaoId}/${tipo}`, corpo);
      return data;
    },
    onSettled: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}
