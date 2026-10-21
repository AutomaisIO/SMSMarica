import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';

/** Sistemas de regulação com fila de guias (ADR-0076). */
export type SistemaFilaGuias = 'Sisreg' | 'Ser' | 'Sernit' | 'EsusSg';

/** `GET …/configuracao/guias` — o mesmo quadro nos quatro sistemas. */
export type FilaGuias = {
  sistema: SistemaFilaGuias;
  habilitada: boolean;
  porHora: number;
  /** "HH:mm" em Brasília; início = fim é o dia inteiro. */
  janelaInicio: string;
  janelaFim: string;
  naFila: number;
  obtidasHoje: number;
  falharam: number;
  ultimaObtidaEm: string | null;
  pausadaAte: string | null;
  /** Por que a fila não está buscando agora (pausa, janela, varredura, orçamento…); nulo = buscando. */
  impedimento: string | null;
  atualizadoEm: string | null;
};

export type SalvarFilaGuias = Pick<FilaGuias, 'habilitada' | 'porHora' | 'janelaInicio' | 'janelaFim'>;

/** Cada sistema expõe a fila ao lado da própria configuração (mesma permissão da tela). */
const ROTA: Record<SistemaFilaGuias, string> = {
  Sisreg: '/sisreg/configuracao/guias',
  Ser: '/regulacao/ser/configuracao/guias',
  Sernit: '/regulacao/sernit/configuracao/guias',
  EsusSg: '/regulacao/esussg/configuracao/guias',
};

export function useFilaGuias(sistema: SistemaFilaGuias) {
  return useQuery({
    queryKey: ['fila-guias', sistema],
    queryFn: async () => (await http.get<FilaGuias>(ROTA[sistema])).data,
    // A fila anda sozinha: os contadores se atualizam enquanto a tela está aberta.
    refetchInterval: 60_000,
  });
}

export function useSalvarFilaGuias(sistema: SistemaFilaGuias) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (c: SalvarFilaGuias) => (await http.put<FilaGuias>(ROTA[sistema], c)).data,
    onSuccess: (dados) => qc.setQueryData(['fila-guias', sistema], dados),
  });
}
