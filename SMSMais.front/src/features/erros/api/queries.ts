import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { buscarErros, obterErroPorCodigo, reabrirErro, resolverErro } from '@/features/erros/api/errosApi';
import type { ErroFiltro } from '@/features/erros/types';

export const errosKeys = {
  raiz: ['erros'] as const,
  busca: (filtro: ErroFiltro) => ['erros', 'busca', filtro] as const,
  porCodigo: (codigo: string) => ['erros', 'codigo', codigo] as const,
};

export function useBuscarErros(filtro: ErroFiltro) {
  return useQuery({
    queryKey: errosKeys.busca(filtro),
    queryFn: () => buscarErros(filtro),
    placeholderData: (anterior) => anterior,
  });
}

export function useErroPorCodigo(codigo: string | null) {
  return useQuery({
    queryKey: codigo ? errosKeys.porCodigo(codigo) : ['erros', 'codigo', 'nenhum'],
    queryFn: () => {
      if (!codigo) throw new Error('Código não informado.');
      return obterErroPorCodigo(codigo);
    },
    enabled: Boolean(codigo),
  });
}

export function useResolverErro() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ codigo, resolvidoPor, nota }: { codigo: string; resolvidoPor?: string; nota?: string }) =>
      resolverErro(codigo, { resolvidoPor, nota }),
    onSuccess: () => qc.invalidateQueries({ queryKey: errosKeys.raiz }),
  });
}

export function useReabrirErro() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (codigo: string) => reabrirErro(codigo),
    onSuccess: () => qc.invalidateQueries({ queryKey: errosKeys.raiz }),
  });
}
