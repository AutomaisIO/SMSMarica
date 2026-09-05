import { useQuery } from '@tanstack/react-query';
import {
  agendaPorDiaSemana,
  agendaSerie,
  detalharDiaAgenda,
  listarDiasAgenda,
  obterOpcoesAgenda,
  obterResumoAgenda,
  rankingAgenda,
  type AgendaFiltro,
} from '@/features/agenda/api/agendaApi';
import {
  listarProcedimentosDemanda,
  obterCobertura,
  obterFaixasEspera,
  obterOpcoesDemanda,
  obterOrigemDemanda,
  obterResumoDemanda,
  obterSerieDemanda,
  type DemandaFiltro,
} from '@/features/agenda/api/demandaApi';

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

// ------------------------------------------------------------------- demanda

export const demandaKeys = {
  cobertura: ['agenda', 'cobertura'] as const,
  opcoes: ['agenda', 'demanda', 'opcoes'] as const,
  resumo: (f: DemandaFiltro) => ['agenda', 'demanda', 'resumo', f] as const,
  procedimentos: (f: DemandaFiltro, o: string) => ['agenda', 'demanda', 'procs', f, o] as const,
  faixas: (f: DemandaFiltro) => ['agenda', 'demanda', 'faixas', f] as const,
  origem: (f: DemandaFiltro, e: string) => ['agenda', 'demanda', 'origem', f, e] as const,
  serie: (f: DemandaFiltro) => ['agenda', 'demanda', 'serie', f] as const,
  serieAgenda: (f: AgendaFiltro) => ['agenda', 'serie', f] as const,
};

/** A cobertura só muda quando a varredura roda — não a cada navegação. */
export function useCoberturaAgenda() {
  return useQuery({ queryKey: demandaKeys.cobertura, queryFn: obterCobertura, staleTime: 5 * 60_000 });
}

export function useOpcoesDemanda() {
  return useQuery({ queryKey: demandaKeys.opcoes, queryFn: obterOpcoesDemanda, staleTime: 5 * 60_000 });
}

export function useResumoDemanda(f: DemandaFiltro) {
  return useQuery({ queryKey: demandaKeys.resumo(f), queryFn: () => obterResumoDemanda(f) });
}

export function useProcedimentosDemanda(f: DemandaFiltro, ordenarPor: 'volume' | 'espera' | 'atraso') {
  return useQuery({
    queryKey: demandaKeys.procedimentos(f, ordenarPor),
    queryFn: () => listarProcedimentosDemanda(f, ordenarPor),
  });
}

export function useFaixasEspera(f: DemandaFiltro) {
  return useQuery({ queryKey: demandaKeys.faixas(f), queryFn: () => obterFaixasEspera(f) });
}

export function useOrigemDemanda(f: DemandaFiltro, eixo: 'solicitante' | 'executante') {
  return useQuery({ queryKey: demandaKeys.origem(f, eixo), queryFn: () => obterOrigemDemanda(f, eixo) });
}

export function useSerieDemanda(f: DemandaFiltro) {
  return useQuery({ queryKey: demandaKeys.serie(f), queryFn: () => obterSerieDemanda(f) });
}

export function useSerieAgenda(f: AgendaFiltro) {
  return useQuery({ queryKey: demandaKeys.serieAgenda(f), queryFn: () => agendaSerie(f) });
}
