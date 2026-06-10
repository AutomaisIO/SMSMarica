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
  LaudoListItem,
  LaudoPorStudy,
} from '@/features/laudos/types';

export async function listarLaudos(filtro: FiltroLaudos): Promise<LaudoListItem[]> {
  const { data } = await http.get<LaudoListItem[]>('/laudos', {
    params: {
      studyInstanceUID: filtro.studyInstanceUID || undefined,
      pacienteId: filtro.pacienteId || undefined,
      medicoId: filtro.medicoId || undefined,
      status: filtro.status || undefined,
      dataInicial: filtro.dataInicial || undefined,
      dataFinal: filtro.dataFinal || undefined,
      limite: filtro.limite ?? 50,
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
