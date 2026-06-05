import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarConfiguracaoSisreg,
  obterConfiguracaoSisreg,
} from '@/features/sisreg/api/sisregApi';
import type { AtualizarSisregConfiguracaoPayload } from '@/features/sisreg/types';

export const sisregKeys = {
  configuracao: ['sisreg', 'configuracao'] as const,
};

export function useConfiguracaoSisreg() {
  return useQuery({
    queryKey: sisregKeys.configuracao,
    queryFn: obterConfiguracaoSisreg,
  });
}

export function useAtualizarConfiguracaoSisreg() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: AtualizarSisregConfiguracaoPayload) => atualizarConfiguracaoSisreg(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: sisregKeys.configuracao }),
  });
}
