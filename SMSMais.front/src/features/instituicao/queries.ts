import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { obterInstituicao, salvarInstituicao, type SalvarInstituicaoPayload } from './api';
import { carregarInstituicao } from '@/shared/tema/instituicao';

const chave = ['instituicao'] as const;

export function useInstituicao() {
  return useQuery({
    queryKey: chave,
    queryFn: obterInstituicao,
  });
}

export function useSalvarInstituicao() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarInstituicaoPayload) => salvarInstituicao(payload),
    onSuccess: async (data) => {
      qc.setQueryData(chave, data);
      // Recarrega a identidade global: cores, título e logo mudam na hora, sem F5.
      // Sem isto, quem acabou de trocar a cor da marca continuaria vendo a antiga e
      // acharia que o salvamento não funcionou.
      await carregarInstituicao();
    },
  });
}
