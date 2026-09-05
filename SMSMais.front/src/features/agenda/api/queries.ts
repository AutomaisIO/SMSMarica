import { useQuery } from '@tanstack/react-query';
import {
  agendaPorDiaSemana,
  detalharDiaAgenda,
  listarDiasAgenda,
  obterOpcoesAgenda,
  obterResumoAgenda,
  rankingAgenda,
  type AgendaFiltro,
} from '@/features/agenda/api/agendaApi';

export const agendaKeys = {
  opcoes: ['agenda', 'opcoes'] as const,
  resumo: (f: AgendaFiltro) => ['agenda', 'resumo', f] as const,
  dias: (f: AgendaFiltro, p: number) => ['agenda', 'dias', f, p] as const,
  dia: (u: string, c: string, d: string) => ['agenda', 'dia', u, c, d] as const,
  ranking: (f: AgendaFiltro, e: string) => ['agenda', 'ranking', f, e] as const,
  diasSemana: (f: AgendaFiltro) => ['agenda', 'dias-semana', f] as const,
};

export function useOpcoesAgenda() {
  // As opções mudam quando a escala é sincronizada, não a cada navegação.
  return useQuery({ queryKey: agendaKeys.opcoes, queryFn: obterOpcoesAgenda, staleTime: 5 * 60_000 });
}

export function useResumoAgenda(f: AgendaFiltro) {
  return useQuery({ queryKey: agendaKeys.resumo(f), queryFn: () => obterResumoAgenda(f) });
}

export function useDiasAgenda(f: AgendaFiltro, pagina: number) {
  return useQuery({ queryKey: agendaKeys.dias(f, pagina), queryFn: () => listarDiasAgenda(f, pagina) });
}

export function useDiaAgenda(unidadeId: string | null, cpf: string | null, data: string | null) {
  return useQuery({
    queryKey: agendaKeys.dia(unidadeId ?? '', cpf ?? '', data ?? ''),
    queryFn: () => detalharDiaAgenda(unidadeId!, cpf!, data!),
    enabled: !!unidadeId && !!cpf && !!data,
  });
}

export function useRankingAgenda(f: AgendaFiltro, eixo: 'unidade' | 'especialidade' | 'profissional') {
  return useQuery({ queryKey: agendaKeys.ranking(f, eixo), queryFn: () => rankingAgenda(f, eixo) });
}

export function useAgendaPorDiaSemana(f: AgendaFiltro) {
  return useQuery({ queryKey: agendaKeys.diasSemana(f), queryFn: () => agendaPorDiaSemana(f) });
}
