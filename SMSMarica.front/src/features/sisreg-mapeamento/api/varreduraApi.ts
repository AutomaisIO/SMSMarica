import { http } from '@/shared/api/httpClient';
import type {
  SalvarVarreduraAgendaPayload,
  StatusVarreduraVivo,
  VarreduraAceita,
  VarreduraAgenda,
  VarreduraExecucao,
} from '@/features/sisreg-mapeamento/types';

/**
 * Motor diário que varre a agenda do SISREG da unidade.
 *
 * Como no mapeamento, o `unidadeId` vai explicitamente no header quando informado — é o que
 * permite operar a unidade da ROTA em `/app/unidades/{id}` sem depender do seletor do topo.
 */
function cabecalhoUnidade(unidadeId?: string | null) {
  return unidadeId ? { headers: { 'X-Unidade-Id': unidadeId } } : undefined;
}

export async function obterVarreduraAgenda(unidadeId?: string | null): Promise<VarreduraAgenda> {
  const { data } = await http.get<VarreduraAgenda>('/sisreg/varredura/agenda', cabecalhoUnidade(unidadeId));
  return data;
}

export async function salvarVarreduraAgenda(
  payload: SalvarVarreduraAgendaPayload,
  unidadeId?: string | null,
): Promise<VarreduraAgenda> {
  const { data } = await http.put<VarreduraAgenda>(
    '/sisreg/varredura/agenda',
    payload,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function executarVarredura(unidadeId?: string | null): Promise<VarreduraAceita> {
  const { data } = await http.post<VarreduraAceita>(
    '/sisreg/varredura/executar',
    undefined,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

/** 204 quando não há varredura em curso — o axios devolve string vazia no data. */
export async function obterStatusVarredura(unidadeId?: string | null): Promise<StatusVarreduraVivo | null> {
  const { data, status } = await http.get<StatusVarreduraVivo | ''>(
    '/sisreg/varredura/status',
    cabecalhoUnidade(unidadeId),
  );
  return status === 204 || !data ? null : data;
}

export async function cancelarVarredura(unidadeId?: string | null): Promise<{ cancelada: boolean }> {
  const { data } = await http.post<{ cancelada: boolean }>(
    '/sisreg/varredura/cancelar',
    undefined,
    cabecalhoUnidade(unidadeId),
  );
  return data;
}

export async function listarVarreduraExecucoes(
  unidadeId?: string | null,
  limite = 20,
): Promise<VarreduraExecucao[]> {
  const { data } = await http.get<VarreduraExecucao[]>('/sisreg/varredura/execucoes', {
    params: { limite },
    ...cabecalhoUnidade(unidadeId),
  });
  return data;
}
