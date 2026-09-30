import { useMutation, useQueryClient } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';
import type {
  AnaliseRegrasDetalhe,
  SistemaAnaliseEspelho,
} from '@/shared/regulacao/analiseRegras/tipos';

/**
 * Refaz agora a análise de regras de um pedido do espelho. Só recalcula o NOSSO parecer — não
 * escreve nada no sistema externo (por isso a rota pede só Consulta do módulo do sistema).
 */
export async function reanalisarAnaliseRegras(
  sistema: SistemaAnaliseEspelho,
  espelhoId: string,
): Promise<AnaliseRegrasDetalhe> {
  const { data } = await http.post<AnaliseRegrasDetalhe>(
    `/regulacao/${sistema}/${espelhoId}/analise/reanalisar`,
  );
  return data;
}

/**
 * Reanalisa e já troca a análise no detalhe em cache.
 *
 * As chaves seguem a convenção das três features (`[sistema, 'solicitacao', id]`,
 * `[sistema, 'busca', …]`, `[sistema, 'resumo']`) e os três detalhes carregam a análise no campo
 * `analise` — é isso que deixa um componente só servir SER, SERNIT e ESUS SG.
 */
export function useReanalisarAnaliseRegras(sistema: SistemaAnaliseEspelho, espelhoId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => reanalisarAnaliseRegras(sistema, espelhoId),
    onSuccess: (analise) => {
      qc.setQueryData<{ analise?: AnaliseRegrasDetalhe | null }>(
        [sistema, 'solicitacao', espelhoId],
        (anterior) => (anterior ? { ...anterior, analise } : anterior),
      );
      // A fila mostra o veredito na linha e conta por veredito nos chips.
      void qc.invalidateQueries({ queryKey: [sistema, 'busca'] });
      void qc.invalidateQueries({ queryKey: [sistema, 'resumo'] });
    },
  });
}
