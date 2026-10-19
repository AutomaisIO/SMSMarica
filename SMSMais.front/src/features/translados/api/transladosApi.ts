import { http } from '@/shared/api/httpClient';
import type {
  AtualizarRotaPayload,
  CadastrarRotaPayload,
  CriarAlocacaoPayload,
  EnderecoParaRevisar,
  EventoViagem,
  FiltrosListarRotas,
  GerarTransladoPayload,
  ResultadoGeracao,
  RotaDiaria,
  Resgates,
  RotaDiariaListItem,
  SessaoElegivel,
} from '@/features/translados/types';

/** Geração automática (FT3): preview (confirmar=false) ou gravar (confirmar=true). */
export async function gerarTranslado(payload: GerarTransladoPayload): Promise<ResultadoGeracao> {
  const { data } = await http.post<ResultadoGeracao>('/translados/gerar', payload);
  return data;
}

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

/** A viagem como o tablet registrou, na ordem em que aconteceu. */
export async function listarViagem(rotaId: string): Promise<EventoViagem[]> {
  const { data } = await http.get<EventoViagem[]>(`/rotas/${rotaId}/viagem`);
  return data;
}

/** Endereços que o Google não localizou (ou localizou mal): ficam fora da geração até alguém
 * marcar o ponto no mapa. */
export async function listarEnderecosParaRevisar(): Promise<EnderecoParaRevisar[]> {
  const { data } = await http.get<EnderecoParaRevisar[]>('/geo/revisao');
  return data;
}

export async function fixarPontoDoEndereco(id: string, latitude: number, longitude: number): Promise<void> {
  await http.post('/geo/fixar', { id, latitude, longitude });
}

/** "Pacientes disponíveis para resgate" (a mesma lista dos tablets), com os lugares livres agora no
 * carro da rota e quem ela já marcou. */
export async function listarResgates(rotaId: string): Promise<Resgates> {
  const { data } = await http.get<Resgates>('/resgates', { params: { rotaId } });
  return data;
}

/** Marca quem a rota vai resgatar (só passa se couber no carro agora). */
export async function marcarResgates(rotaId: string, sessaoIds: string[]): Promise<void> {
  await http.post(`/rotas/${rotaId}/resgates`, { sessaoIds });
}

/** Tira do resgate da rota (antes de embarcar): o paciente volta para a lista. */
export async function desmarcarResgate(rotaId: string, sessaoId: string): Promise<void> {
  await http.delete(`/rotas/${rotaId}/resgates/${sessaoId}`);
}

/** A central avisa que o paciente já está no local esperando. */
export async function avisarNoLocal(sessaoId: string): Promise<void> {
  await http.post(`/resgates/${sessaoId}/no-local`);
}
