import { http } from '@/shared/api/httpClient';
import type {
  RespostaRapida,
  SalvarRespostaRapidaPayload,
  TagAutomatica,
  TextoResolvido,
} from '@/features/respostas-rapidas/types';

export async function listarRespostasRapidas(incluirInativas = false): Promise<RespostaRapida[]> {
  const { data } = await http.get<RespostaRapida[]>('/respostas-rapidas', {
    params: { incluirInativas },
  });
  return data;
}

export async function obterRespostaRapida(id: string): Promise<RespostaRapida> {
  const { data } = await http.get<RespostaRapida>(`/respostas-rapidas/${id}`);
  return data;
}

export async function listarTagsAutomaticas(): Promise<TagAutomatica[]> {
  const { data } = await http.get<TagAutomatica[]>('/respostas-rapidas/tags');
  return data;
}

export async function criarRespostaRapida(payload: SalvarRespostaRapidaPayload): Promise<string> {
  const { data } = await http.post<string>('/respostas-rapidas', payload);
  return data;
}

export async function atualizarRespostaRapida(
  id: string,
  payload: SalvarRespostaRapidaPayload,
): Promise<void> {
  await http.put(`/respostas-rapidas/${id}`, payload);
}

export async function excluirRespostaRapida(id: string): Promise<void> {
  await http.delete(`/respostas-rapidas/${id}`);
}

/** Monta o texto no contexto da conversa. Não envia nada — o resultado vai para o composer. */
export async function resolverRespostaRapida(
  conversaId: string,
  respostaId: string,
  valores: Record<string, string>,
): Promise<TextoResolvido> {
  const { data } = await http.post<TextoResolvido>(
    `/conversas/${conversaId}/respostas-rapidas/${respostaId}/resolver`,
    { valores },
  );
  return data;
}
