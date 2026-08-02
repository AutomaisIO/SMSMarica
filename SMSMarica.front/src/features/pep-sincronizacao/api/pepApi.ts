import { http } from '@/shared/api/httpClient';
import type {
  AgendaPep,
  BasePep,
  DiagnosticoPep,
  DivergenciaIdentidade,
  ExecucaoImportacao,
  IniciarImportacaoPayload,
  ResultadoVerificacaoDivergencias,
  ResumoDivergencias,
  SalvarAgendaPayload,
  StatusDivergencia,
  StatusImportacao,
} from '@/features/pep-sincronizacao/types';

export async function listarBasesPep(): Promise<BasePep[]> {
  const { data } = await http.get<BasePep[]>('/pep-sincronizacao/bases');
  return data;
}

export async function iniciarImportacaoPep(
  payload: IniciarImportacaoPayload,
): Promise<{ execucaoId: string }> {
  const { data } = await http.post<{ execucaoId: string }>('/pep-sincronizacao/importar', payload);
  return data;
}

export async function obterStatusPep(): Promise<StatusImportacao> {
  const { data } = await http.get<StatusImportacao>('/pep-sincronizacao/status');
  return data;
}

export async function cancelarImportacaoPep(pausarHoras?: number): Promise<void> {
  await http.post('/pep-sincronizacao/cancelar', null, {
    params: pausarHoras ? { pausarHoras } : undefined,
  });
}

/** Pausa (horas > 0) ou retoma (horas ausente/0) o motor de sincronismo de uma base. */
export async function pausarMotorPep(fonteId: string, horas?: number): Promise<AgendaPep> {
  const { data } = await http.post<AgendaPep>('/pep-sincronizacao/agenda/pausar', {
    fonteId,
    horas: horas ?? null,
  });
  return data;
}

export async function listarExecucoesPep(fonteId?: string): Promise<ExecucaoImportacao[]> {
  const { data } = await http.get<ExecucaoImportacao[]>('/pep-sincronizacao/execucoes', {
    params: fonteId ? { fonteId } : undefined,
  });
  return data;
}

export async function listarAgendasPep(): Promise<AgendaPep[]> {
  const { data } = await http.get<AgendaPep[]>('/pep-sincronizacao/agenda');
  return data;
}

export async function salvarAgendaPep(payload: SalvarAgendaPayload): Promise<AgendaPep> {
  const { data } = await http.put<AgendaPep>('/pep-sincronizacao/agenda', payload);
  return data;
}

export async function obterDiagnosticoPep(fonteId: string): Promise<DiagnosticoPep> {
  const { data } = await http.get<DiagnosticoPep>('/pep-sincronizacao/diagnostico', {
    params: { fonteId },
  });
  return data;
}

export async function listarDivergenciasPep(
  fonteId?: string,
  status?: StatusDivergencia,
): Promise<DivergenciaIdentidade[]> {
  const { data } = await http.get<DivergenciaIdentidade[]>('/pep-sincronizacao/divergencias', {
    params: { ...(fonteId ? { fonteId } : {}), ...(status ? { status } : {}) },
  });
  return data;
}

export async function obterResumoDivergenciasPep(fonteId?: string): Promise<ResumoDivergencias> {
  const { data } = await http.get<ResumoDivergencias>('/pep-sincronizacao/divergencias/resumo', {
    params: fonteId ? { fonteId } : undefined,
  });
  return data;
}

export async function verificarDivergenciasPep(
  fonteId?: string,
  max?: number,
): Promise<ResultadoVerificacaoDivergencias> {
  const { data } = await http.post<ResultadoVerificacaoDivergencias>(
    '/pep-sincronizacao/divergencias/verificar',
    { fonteId: fonteId ?? null, max: max ?? null },
  );
  return data;
}

export async function ignorarDivergenciaPep(
  id: string,
  motivo?: string,
): Promise<DivergenciaIdentidade> {
  const { data } = await http.post<DivergenciaIdentidade>(
    `/pep-sincronizacao/divergencias/${id}/ignorar`,
    { motivo: motivo ?? null },
  );
  return data;
}
