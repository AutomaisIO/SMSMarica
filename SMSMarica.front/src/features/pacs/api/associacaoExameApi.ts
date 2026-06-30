import { http } from '@/shared/api/httpClient';
import type { AssociacaoExame } from '@/features/pacs/types';
import type { SolicitacaoExame } from '@/features/solicitacoes-exame/types';

/** Vínculos (explícito ou implícito) por StudyInstanceUID, para a listagem. */
export async function listarAssociacoesPorStudies(uids: string[]): Promise<AssociacaoExame[]> {
  if (uids.length === 0) return [];
  // O backend aceita CSV — evita query enorme com vários `studyUIDs=` repetidos.
  const { data } = await http.get<AssociacaoExame[]>('/exames/associacoes', {
    params: { studyUIDs: uids.join(',') },
  });
  return data;
}

export type AssociarExamePayload = {
  studyInstanceUID: string;
  accessionNumber: string;
  accessionNumberDicomOriginal?: string | null;
};

export async function associarExame(payload: AssociarExamePayload): Promise<AssociacaoExame> {
  const { data } = await http.post<AssociacaoExame>('/exames/associacoes', payload);
  return data;
}

export async function desassociarExame(studyInstanceUID: string): Promise<void> {
  await http.delete(`/exames/associacoes/${encodeURIComponent(studyInstanceUID)}`);
}

export type ResincronizacaoResultado = {
  /** Solicitações abertas sem associação ativa (universo da varredura). */
  candidatas: number;
  /** Quantas foram efetivamente consultadas no PACS (≤ candidatas, limitado pelo teto). */
  varridas: number;
  /** Exames associados nesta passagem. */
  associadas: number;
  /** Varridas que ainda não têm exame correspondente no PACS (nada a fazer). */
  semExameNoPacs: number;
  /** Falhas pontuais (consulta ao PACS ou conflito de associação). */
  falhas: number;
  /** True se havia mais candidatas que o teto e a varredura foi truncada. */
  limiteAtingido: boolean;
};

/** Rede de segurança: varre órfãos e associa pelo nº da solicitação no Patient ID. Idempotente. */
export async function resincronizarExames(): Promise<ResincronizacaoResultado> {
  const { data } = await http.post<ResincronizacaoResultado>('/exames/associacoes/resincronizar');
  return data;
}

/** Pré-visualização do modal: solicitação enriquecida pelo número SMS. 204 → null. */
export async function previewSolicitacaoPorAccession(accession: string): Promise<SolicitacaoExame | null> {
  const { data } = await http.get<SolicitacaoExame | ''>(
    `/exames/associacoes/preview/${encodeURIComponent(accession)}`,
  );
  return data && typeof data === 'object' ? data : null;
}
