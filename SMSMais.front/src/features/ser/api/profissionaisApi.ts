import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { http } from '@/shared/api/httpClient';

/** Profissional como está no SER (Cadastro → Profissionais) — espelho à parte (ADR-0065). */
export type SerProfissional = {
  id: string;
  nome: string;
  cpf: string | null;
  documento: string | null;
  tipoDocumento: string | null;
  ativo: boolean;
  /** Quantas linhas do SER caíram neste cadastro (o SER tem duplicados). */
  ocorrencias: number;
  presenteNoSer: boolean;
  ultimaLeituraEm: string;
  medicoId: string | null;
  medicoNome: string | null;
  ligadoEm: string | null;
};

export type PaginaSerProfissionais = {
  itens: SerProfissional[];
  total: number;
  pagina: number;
  tamanhoPagina: number;
};

export type ResumoSerProfissionais = {
  total: number;
  ativos: number;
  comCpf: number;
  ligados: number;
  foraDoSer: number;
  ultimaLeituraEm: string | null;
  importacaoEmExecucao: boolean;
  ultimoErro: string | null;
};

export type FiltroSerProfissionais = {
  termo?: string;
  /** `ativos`, `inativos`, `fora` ou vazio. */
  situacao?: string;
  /** `ligados`, `soltos` ou vazio. */
  ligacao?: string;
  pagina: number;
  tamanhoPagina: number;
};

const base = '/regulacao/ser/profissionais';
const raiz = ['ser', 'profissionais'] as const;

export function useSerProfissionais(filtro: FiltroSerProfissionais) {
  return useQuery({
    queryKey: [...raiz, 'lista', filtro],
    queryFn: async () => (await http.get<PaginaSerProfissionais>(base, { params: filtro })).data,
    placeholderData: (anterior) => anterior,
  });
}

/** O resumo também diz se a importação está rodando — enquanto estiver, relê a cada 5 s. */
export function useResumoSerProfissionais() {
  return useQuery({
    queryKey: [...raiz, 'resumo'],
    queryFn: async () => (await http.get<ResumoSerProfissionais>(`${base}/resumo`)).data,
    refetchInterval: (q) => (q.state.data?.importacaoEmExecucao ? 5000 : false),
  });
}

export function useImportarSerProfissionais() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await http.post(`${base}/importar`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}

export function useLigarSerProfissional() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, medicoId }: { id: string; medicoId: string }) =>
      (await http.put<SerProfissional>(`${base}/${id}/medico`, { medicoId })).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}

export function useDesligarSerProfissional() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await http.delete<SerProfissional>(`${base}/${id}/medico`)).data,
    onSuccess: () => qc.invalidateQueries({ queryKey: raiz }),
  });
}
