import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelarImportacaoPep,
  iniciarImportacaoPep,
  ignorarDivergenciaPep,
  listarDivergenciasPep,
  listarAgendasPep,
  listarBasesPep,
  listarExecucoesPep,
  obterDiagnosticoPep,
  obterResumoDivergenciasPep,
  obterStatusPep,
  pausarMotorPep,
  salvarAgendaPep,
  verificarDivergenciasPep,
} from '@/features/pep-sincronizacao/api/pepApi';
import type {
  IniciarImportacaoPayload,
  SalvarAgendaPayload,
  StatusDivergencia,
} from '@/features/pep-sincronizacao/types';

export const pepKeys = {
  bases: ['pep', 'bases'] as const,
  status: ['pep', 'status'] as const,
  execucoes: (fonteId?: string) => ['pep', 'execucoes', fonteId ?? 'todas'] as const,
  agendas: ['pep', 'agendas'] as const,
  diagnostico: (fonteId: string) => ['pep', 'diagnostico', fonteId] as const,
  divergencias: (fonteId?: string, status?: string) =>
    ['pep', 'divergencias', fonteId ?? 'todas', status ?? 'todos'] as const,
  divergenciasResumo: (fonteId?: string) => ['pep', 'divergencias', 'resumo', fonteId ?? 'todas'] as const,
};

export function useBasesPep() {
  return useQuery({ queryKey: pepKeys.bases, queryFn: listarBasesPep });
}

/** Status com polling em tempo real: 1s enquanto há run vivo, senão a cada 15s. */
export function useStatusPep() {
  return useQuery({
    queryKey: pepKeys.status,
    queryFn: obterStatusPep,
    refetchInterval: (query) => (query.state.data?.emExecucao ? 1000 : 15000),
  });
}

export function useExecucoesPep(fonteId?: string) {
  return useQuery({
    queryKey: pepKeys.execucoes(fonteId),
    queryFn: () => listarExecucoesPep(fonteId),
  });
}

export function useIniciarImportacaoPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: IniciarImportacaoPayload) => iniciarImportacaoPep(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pepKeys.status });
      client.invalidateQueries({ queryKey: ['pep', 'execucoes'] });
    },
  });
}

export function useCancelarImportacaoPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (pausarHoras?: number) => cancelarImportacaoPep(pausarHoras),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pepKeys.status });
      client.invalidateQueries({ queryKey: pepKeys.agendas });
    },
  });
}

/** Pausa/retoma o motor de um clique — em incidente, parar sem pausar não segura nada. */
export function usePausarMotorPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ fonteId, horas }: { fonteId: string; horas?: number }) =>
      pausarMotorPep(fonteId, horas),
    onSuccess: () => client.invalidateQueries({ queryKey: pepKeys.agendas }),
  });
}

export function useAgendasPep() {
  return useQuery({ queryKey: pepKeys.agendas, queryFn: listarAgendasPep, refetchInterval: 30000 });
}

export function useSalvarAgendaPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: SalvarAgendaPayload) => salvarAgendaPep(payload),
    onSuccess: () => client.invalidateQueries({ queryKey: pepKeys.agendas }),
  });
}

/** Diagnóstico sob demanda (abre conexão Oracle) — só busca quando o operador pede. */
export function useDiagnosticoPep(fonteId: string) {
  return useQuery({
    queryKey: pepKeys.diagnostico(fonteId),
    queryFn: () => obterDiagnosticoPep(fonteId),
    enabled: false,
    staleTime: 60000,
  });
}

/** Relatório de divergências de identidade (origem × hub para o mesmo CPF). */
export function useDivergenciasPep(fonteId?: string, status?: StatusDivergencia) {
  return useQuery({
    queryKey: pepKeys.divergencias(fonteId, status),
    queryFn: () => listarDivergenciasPep(fonteId, status),
  });
}

export function useResumoDivergenciasPep(fonteId?: string) {
  return useQuery({
    queryKey: pepKeys.divergenciasResumo(fonteId),
    queryFn: () => obterResumoDivergenciasPep(fonteId),
    refetchInterval: 60000,
  });
}

/** Arbitragem manual: consulta paga, então é sempre disparo explícito do operador. */
export function useVerificarDivergenciasPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ fonteId, max }: { fonteId?: string; max?: number }) =>
      verificarDivergenciasPep(fonteId, max),
    onSuccess: () => client.invalidateQueries({ queryKey: ['pep', 'divergencias'] }),
  });
}

export function useIgnorarDivergenciaPep() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo?: string }) => ignorarDivergenciaPep(id, motivo),
    onSuccess: () => client.invalidateQueries({ queryKey: ['pep', 'divergencias'] }),
  });
}
