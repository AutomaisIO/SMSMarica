import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { obterContextoAnamnese, salvarAnamnese } from '@/features/anamnese/api/anamneseApi';
import {
  criarTokenAnexo,
  excluirAnexo,
  listarAnexosExame,
  salvarAnexo,
} from '@/features/anamnese/api/anexosApi';
import type { SalvarAnamnesePayload, SalvarAnexoPayload } from '@/features/anamnese/types';

export const anamneseKeys = {
  contexto: (chave: string) => ['anamnese', 'contexto', chave] as const,
};

export const anexosKeys = {
  lista: (solicitacaoExameId: string) => ['anexos-exame', solicitacaoExameId] as const,
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

// ---- Anexos de exame (ponte QR → PWA "Arquivos Saúde Maricá") ----

/**
 * Lista os documentos anexados à solicitação. Passe `polling: true` enquanto o
 * modal do QR estiver aberto para que os documentos recém-enviados pelo PWA
 * apareçam quase em tempo real (refetch a cada 3s).
 */
export function useAnexosExame(
  solicitacaoExameId: string | undefined,
  opts?: { polling?: boolean },
) {
  return useQuery({
    queryKey: anexosKeys.lista(solicitacaoExameId ?? ''),
    queryFn: () => listarAnexosExame(solicitacaoExameId as string),
    enabled: Boolean(solicitacaoExameId),
    refetchInterval: opts?.polling ? 3_000 : false,
  });
}

export function useCriarTokenAnexo() {
  return useMutation({
    mutationFn: (solicitacaoExameId: string) => criarTokenAnexo(solicitacaoExameId),
  });
}

export function useSalvarAnexo(solicitacaoExameId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: SalvarAnexoPayload }) =>
      salvarAnexo(id, payload),
    onSuccess: () => {
      if (solicitacaoExameId) {
        client.invalidateQueries({ queryKey: anexosKeys.lista(solicitacaoExameId) });
      }
    },
  });
}

export function useExcluirAnexo(solicitacaoExameId: string | undefined) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => excluirAnexo(id),
    onSuccess: () => {
      if (solicitacaoExameId) {
        client.invalidateQueries({ queryKey: anexosKeys.lista(solicitacaoExameId) });
      }
    },
  });
}
