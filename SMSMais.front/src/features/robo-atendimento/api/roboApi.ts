import { http } from '@/shared/api/httpClient';
import type {
  ComandoRoboCatalogo,
  RoboAssunto,
  RoboAssuntoListItem,
  RoboConfiguracao,
  RoboErro,
  RoboSimulacao,
  SimularRoboPayload,
  SalvarRoboAssuntoPayload,
  StatusRoboErro,
  StatusTreinamento,
  TreinamentoItem,
  TreinamentoItemResumo,
  TreinamentoSimulacao,
} from '@/features/robo-atendimento/types';

export async function listarAssuntos(incluirInativos = false): Promise<RoboAssuntoListItem[]> {
  const { data } = await http.get<RoboAssuntoListItem[]>('/robo/assuntos', {
    params: { incluirInativos: incluirInativos ? 'true' : undefined },
  });
  return data;
}

export async function obterAssunto(id: string): Promise<RoboAssunto> {
  const { data } = await http.get<RoboAssunto>(`/robo/assuntos/${id}`);
  return data;
}

export async function catalogoComandos(): Promise<ComandoRoboCatalogo[]> {
  const { data } = await http.get<ComandoRoboCatalogo[]>('/robo/assuntos/catalogo-comandos');
  return data;
}

export async function criarAssunto(payload: SalvarRoboAssuntoPayload): Promise<string> {
  const { data } = await http.post<string>('/robo/assuntos', payload);
  return data;
}

export async function atualizarAssunto(id: string, payload: SalvarRoboAssuntoPayload): Promise<void> {
  await http.put(`/robo/assuntos/${id}`, payload);
}

export async function excluirAssunto(id: string): Promise<void> {
  await http.delete(`/robo/assuntos/${id}`);
}

export async function obterConfiguracao(): Promise<RoboConfiguracao> {
  const { data } = await http.get<RoboConfiguracao>('/robo/configuracao');
  return data;
}

export async function salvarConfiguracao(payload: RoboConfiguracao): Promise<void> {
  await http.put('/robo/configuracao', payload);
}

/** Ensaia um turno do robô: nada é enviado ao cidadão e comandos de escrita não executam. */
export async function simularRobo(payload: SimularRoboPayload): Promise<RoboSimulacao> {
  const { data } = await http.post<RoboSimulacao>('/robo/simular', payload);
  return data;
}

export async function listarErrosRobo(status?: StatusRoboErro): Promise<RoboErro[]> {
  const { data } = await http.get<RoboErro[]>('/robo/erros', {
    params: status ? { status } : undefined,
  });
  return data;
}

export async function revisarErroRobo(
  id: string,
  payload: { status: 'Revisado' | 'Descartado'; nota?: string | null },
): Promise<void> {
  await http.post(`/robo/erros/${id}/revisar`, payload);
}

// ---- Treinamento ----

export async function listarTreinamento(
  status?: StatusTreinamento,
  assuntoId?: string,
): Promise<TreinamentoItemResumo[]> {
  const { data } = await http.get<TreinamentoItemResumo[]>('/robo/treinamento', {
    params: { status: status || undefined, assuntoId: assuntoId || undefined },
  });
  return data;
}

export async function obterTreinamento(id: string): Promise<TreinamentoItem> {
  const { data } = await http.get<TreinamentoItem>(`/robo/treinamento/${id}`);
  return data;
}

export async function abrirTreinamento(payload: {
  critica: string;
  roboAssuntoId?: string | null;
}): Promise<string> {
  const { data } = await http.post<string>('/robo/treinamento', payload);
  return data;
}

/** Enfileira a análise: o ciclo roda em segundo plano e a tela acompanha pelo status. */
export async function treinarItem(id: string, observacao?: string | null): Promise<void> {
  await http.post(`/robo/treinamento/${id}/treinar`, { observacao: observacao || null });
}

export async function responderPendenciaTreinamento(
  pendenciaId: string,
  payload: { resposta: string; autorizado?: boolean | null },
): Promise<void> {
  await http.post(`/robo/treinamento/pendencias/${pendenciaId}/responder`, payload);
}

export async function dispensarPendenciaTreinamento(
  pendenciaId: string,
  motivo?: string | null,
): Promise<void> {
  await http.post(`/robo/treinamento/pendencias/${pendenciaId}/dispensar`, { motivo: motivo || null });
}

export async function desfazerAlteracaoTreinamento(alteracaoId: string): Promise<void> {
  await http.post(`/robo/treinamento/alteracoes/${alteracaoId}/desfazer`, {});
}

/** Simula a situação contra o modelo treinado ATUAL. Sem mensagem, repete o caso original. */
export async function simularTreinamento(
  id: string,
  payload?: { mensagem?: string | null },
): Promise<TreinamentoSimulacao> {
  const { data } = await http.post<TreinamentoSimulacao>(
    `/robo/treinamento/${id}/simular`,
    payload ?? {},
  );
  return data;
}

export async function descartarTreinamento(id: string): Promise<void> {
  await http.post(`/robo/treinamento/${id}/descartar`, {});
}
