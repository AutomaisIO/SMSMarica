import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarConfiguracao,
  atualizarFonteConfig,
  criarFonteConfig,
  listarFontes,
  listarFontesConfig,
  obterConfiguracao,
  perguntar,
  removerFonteConfig,
  reportarRespostaErrada,
  testarConexaoFonte,
} from '@/features/ia/api/iaApi';
import type {
  AtualizarConfiguracaoPayload,
  PerguntarPayload,
  SalvarFonteConfigPayload,
} from '@/features/ia/types';

export const iaKeys = {
  raiz: ['ia'] as const,
  fontes: ['ia', 'fontes'] as const,
  configuracao: ['ia', 'configuracao'] as const,
  fontesConfig: ['ia', 'configuracao', 'fontes'] as const,
};

export function useFontes() {
  return useQuery({
    queryKey: iaKeys.fontes,
    queryFn: listarFontes,
  });
}

export function usePerguntar() {
  return useMutation({
    mutationFn: (payload: PerguntarPayload) => perguntar(payload),
  });
}

export function useReportarRespostaErrada() {
  return useMutation({
    mutationFn: ({ consultaId, comentario }: { consultaId: string; comentario?: string }) =>
      reportarRespostaErrada(consultaId, comentario),
  });
}

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
