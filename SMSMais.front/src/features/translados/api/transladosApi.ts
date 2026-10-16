import { http } from '@/shared/api/httpClient';
import type {
  AtualizarRotaPayload,
  CadastrarRotaPayload,
  CriarAlocacaoPayload,
  EnderecoParaRevisar,
  EventoViagem,
  PacienteAguardando,
  FiltrosListarRotas,
  GerarTransladoPayload,
  ResultadoGeracao,
  RotaDiaria,
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

/** Quem aguarda a volta (fila de "puxar"). */
export async function listarAguardandoRetorno(): Promise<PacienteAguardando[]> {
  const { data } = await http.get<PacienteAguardando[]>('/rastreamento/aguardando');
  return data;
}

/** Põe a volta do paciente na rota: busca na unidade e entrega em casa. */
export async function puxarParaRota(rotaId: string, sessaoId: string): Promise<void> {
  await http.post('/rastreamento/puxar', { rotaId, sessaoId });
}
