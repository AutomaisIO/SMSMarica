import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  obterLaudoConfiguracao,
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

export function useSalvarLaudoConfiguracao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarLaudoConfiguracaoPayload) => salvarLaudoConfiguracao(payload),
    onSuccess: (data) => qc.setQueryData(chave, data),
  });
}
