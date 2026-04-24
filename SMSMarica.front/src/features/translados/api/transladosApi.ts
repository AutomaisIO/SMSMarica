import { http } from '@/shared/api/httpClient';
import type {
  AtualizarRotaPayload,
  CadastrarRotaPayload,
  CriarAlocacaoPayload,
  FiltrosListarRotas,
  RotaDiaria,
  RotaDiariaListItem,
  SessaoElegivel,
} from '@/features/translados/types';

export async function listarRotas(filtros: FiltrosListarRotas = {}): Promise<RotaDiariaListItem[]> {
  const params: Record<string, string> = {};
  if (filtros.data) params.data = filtros.data;
  if (filtros.motoristaId) params.motoristaId = filtros.motoristaId;
  if (filtros.veiculoId) params.veiculoId = filtros.veiculoId;
  const { data } = await http.get<RotaDiariaListItem[]>('/rotas', {
    params: Object.keys(params).length ? params : undefined,
  });
  return data;
}

export async function obterRotaPorId(id: string): Promise<RotaDiaria> {
  const { data } = await http.get<RotaDiaria>(`/rotas/${id}`);
  return data;
}

export async function cadastrarRota(payload: CadastrarRotaPayload): Promise<string> {
  const { data } = await http.post<string>('/rotas', payload);
  return data;
}

export async function atualizarRota(id: string, payload: AtualizarRotaPayload): Promise<void> {
  await http.put(`/rotas/${id}`, payload);
}

export async function iniciarRota(id: string): Promise<void> {
  await http.post(`/rotas/${id}/iniciar`);
}

export async function concluirRota(id: string): Promise<void> {
  await http.post(`/rotas/${id}/concluir`);
}

export async function cancelarRota(id: string): Promise<void> {
  await http.delete(`/rotas/${id}`);
}

export async function listarSessoesElegiveis(rotaId: string): Promise<SessaoElegivel[]> {
  const { data } = await http.get<SessaoElegivel[]>(`/rotas/${rotaId}/sessoes-elegiveis`);
  return data;
}

export async function criarAlocacao(rotaId: string, payload: CriarAlocacaoPayload): Promise<string> {
  const { data } = await http.post<string>(`/rotas/${rotaId}/alocacoes`, payload);
  return data;
}

export async function removerAlocacao(rotaId: string, alocacaoId: string): Promise<void> {
  await http.delete(`/rotas/${rotaId}/alocacoes/${alocacaoId}`);
}
