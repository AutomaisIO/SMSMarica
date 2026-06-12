import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { obterContextoAnamnese, salvarAnamnese } from '@/features/anamnese/api/anamneseApi';
import type { SalvarAnamnesePayload } from '@/features/anamnese/types';

export const anamneseKeys = {
  contexto: (chave: string) => ['anamnese', 'contexto', chave] as const,
};

/** Carrega o contexto por solicitação OU accession (o que estiver disponível). */
export function useContextoAnamnese(params: {
  solicitacaoExameId?: string;
  accessionNumber?: string;
}) {
  const chave = params.solicitacaoExameId ?? params.accessionNumber ?? '';
  return useQuery({
    queryKey: anamneseKeys.contexto(chave),
    queryFn: () => obterContextoAnamnese(params),
    enabled: Boolean(chave),
  });
}

export function useSalvarAnamnese() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      solicitacaoExameId,
      payload,
    }: {
      solicitacaoExameId: string;
      payload: SalvarAnamnesePayload;
    }) => salvarAnamnese(solicitacaoExameId, payload),
    onSuccess: () => client.invalidateQueries({ queryKey: ['anamnese'] }),
  });
}
