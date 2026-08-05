import { http } from '@/shared/api/httpClient';
import type {
  BuscaSerFiltro,
  BuscaSerResultado,
  DispararVarreduraPayload,
  ExecucaoSer,
  ResumoSituacaoSer,
  SolicitacaoSerDetalhe,
  StatusMotorSer,
} from '@/features/ser/types';

/** Busca na NOSSA base espelhada — não vai ao SER. */
export async function buscarSolicitacoesSer(filtro: BuscaSerFiltro): Promise<BuscaSerResultado> {
  const { data } = await http.get<BuscaSerResultado>('/regulacao/ser', { params: filtro });
  return data;
}

export async function obterResumoSer(): Promise<ResumoSituacaoSer[]> {
  const { data } = await http.get<ResumoSituacaoSer[]>('/regulacao/ser/resumo');
  return data;
}

export async function obterSolicitacaoSer(id: string): Promise<SolicitacaoSerDetalhe> {
  const { data } = await http.get<SolicitacaoSerDetalhe>(`/regulacao/ser/${id}`);
  return data;
}

export async function obterStatusMotorSer(): Promise<StatusMotorSer> {
  const { data } = await http.get<StatusMotorSer>('/regulacao/ser/configuracao/status');
  return data;
}

export async function listarExecucoesSer(limite = 20): Promise<ExecucaoSer[]> {
  const { data } = await http.get<ExecucaoSer[]>('/regulacao/ser/configuracao/execucoes', {
    params: { limite },
  });
  return data;
}

/** Enfileira a varredura. Responde 202 — a rodada leva de 15 min a ~1 h e roda em background. */
export async function dispararVarreduraSer(payload: DispararVarreduraPayload): Promise<void> {
  await http.post('/regulacao/ser/configuracao/varreduras', payload);
}

/** Testa a credencial contra o SER sem gravá-la (nenhuma escrita no SER). */
export async function testarCredencialSer(usuario: string, senha: string): Promise<void> {
  await http.post('/regulacao/ser/configuracao/testar-credencial', { usuario, senha });
}

/** Grava a credencial (cifrada). O backend autentica antes de persistir. */
export async function salvarCredencialSer(usuario: string, senha: string): Promise<void> {
  await http.put('/regulacao/ser/configuracao/credencial', { usuario, senha });
}
