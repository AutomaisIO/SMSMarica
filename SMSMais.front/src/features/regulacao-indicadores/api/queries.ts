import { useQuery } from '@tanstack/react-query';
import type { FonteIndicadores, PeriodoMeses } from '../types';
import { obterIndicadores } from './indicadoresApi';

export const indicadoresKeys = {
  todos: ['regulacao', 'indicadores'] as const,
  fonte: (fonte: FonteIndicadores, periodo: PeriodoMeses) =>
    ['regulacao', 'indicadores', fonte, periodo.inicio, periodo.fim] as const,
};

/** Série mensal cara de montar e que só muda com a varredura: não refaz a cada foco de janela. */
const TEMPO_FRESCO = 5 * 60_000;

export function useIndicadoresRegulacao(fonte: FonteIndicadores, periodo: PeriodoMeses, habilitado = true) {
  return useQuery({
    queryKey: indicadoresKeys.fonte(fonte, periodo),
    queryFn: () => obterIndicadores(fonte, periodo),
    staleTime: TEMPO_FRESCO,
    enabled: habilitado,
    // Trocar o período mantém a tela anterior até a nova chegar — mas só dentro do mesmo sistema:
    // mostrar os números do SER sob o título do SISREG, nem por um instante.
    placeholderData: (anterior, consultaAnterior) =>
      consultaAnterior?.queryKey[2] === fonte ? anterior : undefined,
  });
}
