import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  obterLaudoConfiguracao,
  obterRegrasIniciarLaudo,
  salvarLaudoConfiguracao,
  type SalvarLaudoConfiguracaoPayload,
} from './api';

const chave = ['laudo-configuracao'] as const;

export function useLaudoConfiguracao() {
  return useQuery({
    queryKey: chave,
    queryFn: obterLaudoConfiguracao,
  });
}

/** Regras de iniciar o laudo (leve, sem permissão de Configuração de Laudo). */
export function useRegrasIniciarLaudo() {
  return useQuery({
    queryKey: ['laudo-regras-iniciar'],
    queryFn: obterRegrasIniciarLaudo,
    staleTime: 60_000,
  });
}

export function useSalvarLaudoConfiguracao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarLaudoConfiguracaoPayload) => salvarLaudoConfiguracao(payload),
    onSuccess: (data) => qc.setQueryData(chave, data),
  });
}
