import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarConfiguracao,
  atualizarFonteConfig,
  criarFonteConfig,
  desativarAprendizado,
  listarAprendizados,
  listarCorrecoes,
  listarFeedbacks,
  listarFontesConfig,
  obterConfiguracao,
  removerFonteConfig,
  testarConexaoFonte,
  tratarFeedback,
} from '@/features/ia/api/iaApi';
import type { AtualizarConfiguracaoPayload, SalvarFonteConfigPayload } from '@/features/ia/types';

export const iaKeys = {
  raiz: ['ia'] as const,
  fontes: ['ia', 'fontes'] as const,
  configuracao: ['ia', 'configuracao'] as const,
  fontesConfig: ['ia', 'configuracao', 'fontes'] as const,
  aprendizados: (fonteId?: string) => ['ia', 'aprendizados', fonteId ?? 'todas'] as const,
  correcoes: (fonteId?: string) => ['ia', 'correcoes', fonteId ?? 'todas'] as const,
  feedbacks: (pendentes: boolean) => ['ia', 'feedbacks', pendentes] as const,
};

// ── Configuração ────────────────────────────────────────────────────────────

export function useConfiguracaoIa() {
  return useQuery({
    queryKey: iaKeys.configuracao,
    queryFn: obterConfiguracao,
  });
}

export function useAtualizarConfiguracaoIa() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarConfiguracaoPayload) => atualizarConfiguracao(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: iaKeys.configuracao });
    },
  });
}

export function useFontesConfig() {
  return useQuery({
    queryKey: iaKeys.fontesConfig,
    queryFn: listarFontesConfig,
  });
}

export function useCriarFonteConfig() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarFonteConfigPayload) => criarFonteConfig(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: iaKeys.fontesConfig });
      client.invalidateQueries({ queryKey: iaKeys.fontes });
    },
  });
}

export function useAtualizarFonteConfig() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarFonteConfigPayload }) =>
      atualizarFonteConfig(id, payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: iaKeys.fontesConfig });
      client.invalidateQueries({ queryKey: iaKeys.fontes });
    },
  });
}

export function useRemoverFonteConfig() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => removerFonteConfig(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: iaKeys.fontesConfig });
      client.invalidateQueries({ queryKey: iaKeys.fontes });
    },
  });
}

export function useTestarConexaoFonte() {
  return useMutation({
    mutationFn: (id: string) => testarConexaoFonte(id),
  });
}

// ── Governança / Melhorias (aprendizado) ─────────────────────────────────────

export function useAprendizados(fonteId?: string) {
  return useQuery({
    queryKey: iaKeys.aprendizados(fonteId),
    queryFn: () => listarAprendizados(fonteId),
  });
}

export function useCorrecoes(fonteId?: string) {
  return useQuery({
    queryKey: iaKeys.correcoes(fonteId),
    queryFn: () => listarCorrecoes(fonteId),
  });
}

export function useDesativarAprendizado() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarAprendizado(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: ['ia', 'aprendizados'] });
      client.invalidateQueries({ queryKey: ['ia', 'correcoes'] });
    },
  });
}

export function useFeedbacks(apenasPendentes = true) {
  return useQuery({
    queryKey: iaKeys.feedbacks(apenasPendentes),
    queryFn: () => listarFeedbacks(apenasPendentes),
  });
}

export function useTratarFeedback() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; descartar: boolean; resolucao?: string }) =>
      tratarFeedback(v.id, v.descartar, v.resolucao),
    onSuccess: () => client.invalidateQueries({ queryKey: ['ia', 'feedbacks'] }),
  });
}
