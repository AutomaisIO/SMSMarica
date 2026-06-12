import { http } from '@/shared/api/httpClient';
import type { AnamneseContexto, AnamneseDto, SalvarAnamnesePayload } from '@/features/anamnese/types';

/** Contexto da tela (pedido + paciente + anamnese existente). Passe UM dos dois. */
export async function obterContextoAnamnese(params: {
  solicitacaoExameId?: string;
  accessionNumber?: string;
}): Promise<AnamneseContexto> {
  const { data } = await http.get<AnamneseContexto>('/anamneses/contexto', { params });
  return data;
}

/** Cria ou atualiza (upsert) a anamnese da solicitação. */
export async function salvarAnamnese(
  solicitacaoExameId: string,
  payload: SalvarAnamnesePayload,
): Promise<AnamneseDto> {
  const { data } = await http.put<AnamneseDto>(`/anamneses/${solicitacaoExameId}`, payload);
  return data;
}
