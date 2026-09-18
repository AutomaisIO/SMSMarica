import { http } from '@/shared/api/httpClient';
import type {
  Estrategia,
  EstrategiaResumo,
  ModoRodada,
  ParametrosEstrategia,
  ProcedimentoComFila,
  Projecao,
  Rodada,
  SimularResposta,
  StatusEstrategia,
} from '@/features/estrategias-fila/types';

const BASE = '/agenda/estrategias';

/** O agente leva de 30 a 90 s; o timeout padrão do cliente mataria a rodada no meio. */
const TIMEOUT_AGENTE_MS = 4 * 60_000;

export async function listarProcedimentos(busca?: string, ordenar?: string): Promise<ProcedimentoComFila[]> {
  const { data } = await http.get<ProcedimentoComFila[]>(`${BASE}/procedimentos`, {
    params: { busca: busca || undefined, ordenar: ordenar || undefined },
  });
  return data;
}

export async function obterCenario(procedimentoCodigo: string | null, procedimentoNome: string): Promise<SimularResposta> {
  const { data } = await http.get<SimularResposta>(`${BASE}/cenario`, {
    params: { procedimentoCodigo: procedimentoCodigo || undefined, procedimentoNome },
  });
  return data;
}

export async function simular(
  procedimentoCodigo: string | null,
  procedimentoNome: string,
  parametros: ParametrosEstrategia,
): Promise<SimularResposta> {
  const { data } = await http.post<SimularResposta>(`${BASE}/simular`, {
    procedimentoCodigo,
    procedimentoNome,
    parametros,
  });
  return data;
}

/** Só a projeção (função pura no servidor) — o que a tela chama a cada clique no quadro. */
export async function projetar(parametros: ParametrosEstrategia, filaInicial: number): Promise<Projecao> {
  const { data } = await http.post<Projecao>(`${BASE}/projetar`, { parametros, filaInicial });
  return data;
}

export async function listarEstrategias(filtro: {
  status?: StatusEstrategia | null;
  procedimentoCodigo?: string | null;
  procedimentoNome?: string | null;
  incluirArquivadas?: boolean;
}): Promise<EstrategiaResumo[]> {
  const { data } = await http.get<EstrategiaResumo[]>(BASE, {
    params: {
      status: filtro.status || undefined,
      procedimentoCodigo: filtro.procedimentoCodigo || undefined,
      procedimentoNome: filtro.procedimentoNome || undefined,
      incluirArquivadas: filtro.incluirArquivadas ? 'true' : undefined,
    },
  });
  return data;
}

export async function obterEstrategia(id: string): Promise<Estrategia> {
  const { data } = await http.get<Estrategia>(`${BASE}/${id}`);
  return data;
}

export async function obterRodada(id: string, numero: number): Promise<Rodada> {
  const { data } = await http.get<Rodada>(`${BASE}/${id}/rodadas/${numero}`);
  return data;
}

export async function criarEstrategia(payload: {
  nome: string;
  procedimentoCodigo: string | null;
  procedimentoNome: string;
  parametros: ParametrosEstrategia;
}): Promise<string> {
  const { data } = await http.post<string>(BASE, payload);
  return data;
}

export async function atualizarEstrategia(
  id: string,
  payload: { nome: string; parametros: ParametrosEstrategia; status?: StatusEstrategia | null },
): Promise<void> {
  await http.put(`${BASE}/${id}`, payload);
}

export async function rodar(id: string, modo: ModoRodada, parametros: ParametrosEstrategia): Promise<Rodada> {
  const { data } = await http.post<Rodada>(
    `${BASE}/${id}/rodadas`,
    { modo, parametros },
    { timeout: modo === 'Agente' ? TIMEOUT_AGENTE_MS : undefined },
  );
  return data;
}

export async function marcarAplicada(id: string, nota: string | null): Promise<void> {
  await http.post(`${BASE}/${id}/marcar-aplicada`, { nota });
}

export async function arquivarEstrategia(id: string): Promise<void> {
  await http.post(`${BASE}/${id}/arquivar`);
}

export async function excluirEstrategia(id: string): Promise<void> {
  await http.delete(`${BASE}/${id}`);
}
