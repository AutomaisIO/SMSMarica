import { http } from '@/shared/api/httpClient';
import type {
  AssinaturaStatus,
  AtualizarLaudoPayload,
  CadastrarLaudoPayload,
  FiltroLaudos,
  FinalizarLaudoPayload,
  IniciarAssinaturaResp,
  Laudo,
  LaudoHistoricoItem,
  LaudoPorStudy,
  PaginaLaudos,
} from '@/features/laudos/types';

export async function listarLaudos(filtro: FiltroLaudos, signal?: AbortSignal): Promise<PaginaLaudos> {
  const { data } = await http.get<PaginaLaudos>('/laudos', {
    signal,
    params: {
      studyInstanceUID: filtro.studyInstanceUID || undefined,
      pacienteId: filtro.pacienteId || undefined,
      medicoId: filtro.medicoId || undefined,
      status: filtro.status || undefined,
      dataInicial: filtro.dataInicial || undefined,
      dataFinal: filtro.dataFinal || undefined,
      biRads: filtro.biRads || undefined,
      vinculado: filtro.vinculado,
      assinado: filtro.assinado,
      termo: filtro.termo || undefined,
      limite: filtro.limite ?? 50,
      pagina: filtro.pagina ?? 1,
    },
  });
  return data;
}

export async function obterLaudo(id: string): Promise<Laudo> {
  const { data } = await http.get<Laudo>(`/laudos/${id}`);
  return data;
}

export async function listarHistorico(id: string): Promise<LaudoHistoricoItem[]> {
  const { data } = await http.get<LaudoHistoricoItem[]>(`/laudos/${id}/historico`);
  return data;
}

export async function listarLaudosPorStudies(uids: string[]): Promise<LaudoPorStudy[]> {
  if (uids.length === 0) return [];
  // O backend aceita CSV — evita query enorme com vários `studyUIDs=` repetidos.
  const { data } = await http.get<LaudoPorStudy[]>('/laudos/por-studies', {
    params: { studyUIDs: uids.join(',') },
  });
  return data;
}

export async function cadastrarLaudo(payload: CadastrarLaudoPayload): Promise<string> {
  const { data } = await http.post<string>('/laudos', payload);
  return data;
}

export async function atualizarLaudo(id: string, payload: AtualizarLaudoPayload): Promise<void> {
  await http.put(`/laudos/${id}`, payload);
}

export async function finalizarLaudo(id: string, payload: FinalizarLaudoPayload): Promise<void> {
  await http.post(`/laudos/${id}/finalizar`, payload);
}

export async function criarNovaVersaoLaudo(id: string): Promise<string> {
  const { data } = await http.post<string>(`/laudos/${id}/nova-versao`);
  return data;
}

export async function excluirLaudo(id: string): Promise<void> {
  await http.delete(`/laudos/${id}`);
}

// ---- Assinatura digital ----

export async function iniciarAssinatura(id: string): Promise<IniciarAssinaturaResp> {
  const { data } = await http.post<IniciarAssinaturaResp>(`/laudos/${id}/assinatura/iniciar`);
  return data;
}

export async function obterStatusAssinatura(id: string): Promise<AssinaturaStatus> {
  const { data } = await http.get<AssinaturaStatus>(`/laudos/${id}/assinatura`);
  return data;
}

/** PDF assinado aguardando a CONFERÊNCIA do médico (preview do modal de aprovação). */
export async function obterPdfAprovacao(id: string): Promise<Blob> {
  const { data } = await http.get(`/laudos/${id}/assinatura/pdf-aprovacao`, {
    responseType: 'blob',
  });
  return data as Blob;
}

/** Aprova o documento assinado: oficializa o laudo e dispara o aviso ao paciente. */
export async function aprovarAssinatura(id: string): Promise<void> {
  await http.post(`/laudos/${id}/assinatura/aprovar`);
}

/** Rejeita na conferência: cancela a assinatura (PDF preservado) e libera assinar de novo. */
export async function rejeitarAssinatura(id: string): Promise<void> {
  await http.post(`/laudos/${id}/assinatura/rejeitar`);
}
