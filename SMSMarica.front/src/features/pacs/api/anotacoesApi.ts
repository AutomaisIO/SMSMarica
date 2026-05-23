import { http } from '@/shared/api/httpClient';
import type {
  EstudoAnotacaoVersao,
  EstudoAnotacaoVersaoResumo,
  PayloadAnotacoes,
} from '@/features/pacs/types';

function rotaBase(studyUID: string): string {
  return `/estudos/${encodeURIComponent(studyUID)}/anotacoes`;
}

/**
 * Versão mais recente das anotações do estudo. Resolve para `null` quando o
 * estudo ainda não foi anotado (a API responde 204 nesse caso).
 */
export async function obterVersaoAtual(studyUID: string): Promise<EstudoAnotacaoVersao | null> {
  const { data, status } = await http.get<EstudoAnotacaoVersao | ''>(rotaBase(studyUID), {
    // Aceita 204 sem disparar interceptor de erro.
    validateStatus: (s) => s === 200 || s === 204,
  });
  if (status === 204 || !data) return null;
  return data as EstudoAnotacaoVersao;
}

/** Histórico completo (mais recente primeiro), sem payload. */
export async function listarHistorico(studyUID: string): Promise<EstudoAnotacaoVersaoResumo[]> {
  const { data } = await http.get<EstudoAnotacaoVersaoResumo[]>(`${rotaBase(studyUID)}/historico`);
  return Array.isArray(data) ? data : [];
}

/** Versão específica com payload (para restaurar/visualizar uma versão antiga). */
export async function obterVersao(studyUID: string, versao: number): Promise<EstudoAnotacaoVersao> {
  const { data } = await http.get<EstudoAnotacaoVersao>(
    `${rotaBase(studyUID)}/versoes/${versao}`,
  );
  return data;
}

/** Cria uma nova versão imutável — o backend carimba usuário/data. */
export async function salvarVersao(
  studyUID: string,
  payload: PayloadAnotacoes,
  comentario?: string | null,
): Promise<EstudoAnotacaoVersao> {
  const { data } = await http.post<EstudoAnotacaoVersao>(rotaBase(studyUID), {
    payload,
    comentario: comentario ?? null,
  });
  return data;
}
