import { useQuery } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';

import type { SistemaRegulacao } from '../types';

export type CidRegulacao = {
  codigo: string;
  descricao: string;
  /** O texto do jeito do SER/SERNIT — `(A09 ) Diarréia...` — e é ele que se guarda. */
  texto: string;
};

export type CidRegulacaoSugestoes = {
  itens: CidRegulacao[];
  /** O sistema cortou a lista no teto dele (500): há mais CID que casam o termo. */
  truncado: boolean;
  sistema: SistemaRegulacao;
};

export async function buscarCidsRegulacao(
  procedimentoId: string,
  sistema: SistemaRegulacao | null,
  termo: string,
): Promise<CidRegulacaoSugestoes> {
  const { data } = await http.get<CidRegulacaoSugestoes>('/regulacao/solicitacoes/formulario/cids', {
    params: { procedimentoId, sistema: sistema ?? undefined, termo },
  });
  return data;
}

/**
 * CID que o destino aceita como Hipótese para o procedimento. O procedimento entra no
 * `enabled` porque é ele (o recurso, no SER) que define a lista; o TERMO não: vazio é pedido
 * válido e lista tudo, como no SER.
 */
export function useCidsRegulacao(
  procedimentoId: string | null,
  sistema: SistemaRegulacao | null,
  termo: string,
) {
  const busca = termo.trim();
  return useQuery({
    queryKey: ['regulacao', 'formulario', 'cids', procedimentoId, sistema, busca],
    queryFn: () => buscarCidsRegulacao(procedimentoId!, sistema, busca),
    enabled: !!procedimentoId,
    // A lista do recurso só muda quando a SES mexe no catálogo.
    staleTime: 6 * 60 * 60 * 1000,
  });
}
