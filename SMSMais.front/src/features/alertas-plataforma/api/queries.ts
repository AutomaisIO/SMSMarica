import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  adicionarDestinatario,
  atualizarDestinatario,
  listarEnviosAlerta,
  obterPainelAlertas,
  removerDestinatario,
  silenciarOrigem,
  testarAlerta,
} from '@/features/alertas-plataforma/api/alertasApi';
import type { SalvarDestinatario } from '@/features/alertas-plataforma/types';

export const alertasKeys = {
  raiz: ['alertas-plataforma'] as const,
  painel: ['alertas-plataforma', 'painel'] as const,
  envios: (origem: string) => ['alertas-plataforma', 'envios', origem] as const,
};

export function usePainelAlertas() {
  return useQuery({
    queryKey: alertasKeys.painel,
    queryFn: obterPainelAlertas,
    // Aviso novo aparece sem recarregar a tela.
    refetchInterval: 30_000,
  });
}

/** Histórico filtrado por fonte (sem filtro, o painel já traz os 100 mais recentes). */
export function useEnviosAlerta(origem: string | null) {
  return useQuery({
    queryKey: alertasKeys.envios(origem ?? ''),
    queryFn: () => listarEnviosAlerta(origem ?? undefined),
    enabled: Boolean(origem),
  });
}

function useInvalidar() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: alertasKeys.raiz });
}

export function useAdicionarDestinatario() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: (b: SalvarDestinatario) => adicionarDestinatario(b), onSuccess: invalidar });
}

export function useAtualizarDestinatario() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: SalvarDestinatario }) => atualizarDestinatario(id, body),
    onSuccess: invalidar,
  });
}

export function useRemoverDestinatario() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: (id: string) => removerDestinatario(id), onSuccess: invalidar });
}

export function useSilenciarOrigem() {
  const invalidar = useInvalidar();
  return useMutation({
    mutationFn: ({ chave, silenciada }: { chave: string; silenciada: boolean }) => silenciarOrigem(chave, silenciada),
    onSuccess: invalidar,
  });
}

export function useTestarAlerta() {
  const invalidar = useInvalidar();
  return useMutation({ mutationFn: testarAlerta, onSuccess: invalidar });
}
