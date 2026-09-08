import { http } from '@/shared/api/httpClient';
import type {
  AdicionarEscopoPayload,
  AtualizarEscopoPayload,
  EscopoExameItem,
  PendenciaEscopoExame,
  ResultadoBackfillEscopo,
} from '@/features/escopo-exames/types';

/**
 * O escopo é sempre pedido com a unidade na ROTA, não pelo header X-Unidade-Id: a mesma tela é
 * usada pelo admin dentro de outra unidade que não a ativa dele. Mesmo motivo do
 * `equipamentosApi`, que filtra por query string.
 */
export async function listarEscopoDaUnidade(
  unidadeId: string,
  incluirInativos = false,
): Promise<EscopoExameItem[]> {
  const { data } = await http.get<EscopoExameItem[]>(`/escopo-exames/unidade/${unidadeId}`, {
    params: { incluirInativos: incluirInativos ? 'true' : undefined },
  });
  return data;
}

export async function listarUnidadesDoTipo(tipoExameId: string): Promise<EscopoExameItem[]> {
  const { data } = await http.get<EscopoExameItem[]>(`/escopo-exames/tipo/${tipoExameId}`);
  return data;
}

export async function listarPendenciasEscopo(unidadeId?: string): Promise<PendenciaEscopoExame[]> {
  const { data } = await http.get<PendenciaEscopoExame[]>('/escopo-exames/pendencias', {
    params: { unidadeId: unidadeId || undefined },
  });
  return data;
}

export async function adicionarEscopo(payload: AdicionarEscopoPayload): Promise<string> {
  const { data } = await http.post<string>('/escopo-exames', {
    tipoExameId: payload.tipoExameId,
    unidadeId: payload.unidadeId,
    enviarParaWorklist: payload.enviarParaWorklist ?? false,
    equipamentoId: payload.equipamentoId ?? null,
  });
  return data;
}

export async function atualizarEscopo(id: string, payload: AtualizarEscopoPayload): Promise<void> {
  await http.put(`/escopo-exames/${id}`, {
    enviarParaWorklist: payload.enviarParaWorklist,
    equipamentoId: payload.equipamentoId,
    ativo: payload.ativo ?? true,
  });
}

export async function removerEscopo(id: string): Promise<void> {
  await http.delete(`/escopo-exames/${id}`);
}

/** Idempotente. `simular` mostra o que aconteceria sem gravar. */
export async function rodarBackfillEscopo(simular: boolean): Promise<ResultadoBackfillEscopo> {
  const { data } = await http.post<ResultadoBackfillEscopo>('/escopo-exames/backfill', null, {
    params: { simular: simular ? 'true' : 'false' },
  });
  return data;
}
