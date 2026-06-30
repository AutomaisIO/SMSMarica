import { http } from '@/shared/api/httpClient';
import type {
  AtualizarSolicitacaoPayload,
  CadastrarSolicitacaoPayload,
  FiltroSolicitacoes,
  SolicitacaoExame,
  SolicitacaoExameListItem,
} from '@/features/solicitacoes-exame/types';

export async function listarSolicitacoes(filtro: FiltroSolicitacoes): Promise<SolicitacaoExameListItem[]> {
  const { data } = await http.get<SolicitacaoExameListItem[]>('/solicitacoes-exame', {
    params: {
      status: filtro.status,
      pacienteId: filtro.pacienteId,
      unidadeId: filtro.unidadeId,
      tipoExameId: filtro.tipoExameId,
      dataInicial: filtro.dataInicial,
      dataFinal: filtro.dataFinal,
      accessionNumber: filtro.accessionNumber,
      limite: filtro.limite ?? 50,
    },
  });
  return data;
}

export async function obterSolicitacao(id: string): Promise<SolicitacaoExame> {
  const { data } = await http.get<SolicitacaoExame>(`/solicitacoes-exame/${id}`);
  return data;
}

export async function obterSolicitacaoPorStudy(studyInstanceUID: string): Promise<SolicitacaoExame | null> {
  const resp = await http.get<SolicitacaoExame>(`/solicitacoes-exame/por-study/${encodeURIComponent(studyInstanceUID)}`, {
    validateStatus: (s) => s === 200 || s === 204,
  });
  return resp.status === 204 ? null : resp.data;
}

export async function cadastrarSolicitacao(payload: CadastrarSolicitacaoPayload): Promise<string> {
  const { data } = await http.post<string>('/solicitacoes-exame', payload);
  return data;
}

export async function atualizarSolicitacao(id: string, payload: AtualizarSolicitacaoPayload): Promise<void> {
  await http.put(`/solicitacoes-exame/${id}`, payload);
}

export async function cancelarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/cancelar`, { motivo });
}

export async function reenviarWorklist(id: string): Promise<void> {
  await http.post(`/solicitacoes-exame/${id}/reenviar-worklist`);
}

export async function excluirSolicitacao(id: string, force = false): Promise<void> {
  await http.delete(`/solicitacoes-exame/${id}`, { params: force ? { force: true } : undefined });
}

/**
 * Abre, em nova aba, o PDF da declaração de comparecimento da solicitação. O
 * endpoint exige bearer (popups não levam o token do interceptor), então baixamos
 * como blob e abrimos uma blob URL.
 */
export async function abrirDeclaracaoComparecimento(id: string): Promise<void> {
  const resp = await http.get(`/solicitacoes-exame/${id}/declaracao-comparecimento`, {
    responseType: 'blob',
  });
  const url = URL.createObjectURL(new Blob([resp.data], { type: 'application/pdf' }));
  const janela = window.open(url, `declaracao-${id}`);
  if (!janela) {
    alert('A janela do documento foi bloqueada pelo navegador. Libere os popups para este site.');
    URL.revokeObjectURL(url);
    return;
  }
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/**
 * True se o erro for o 409 de falha ao remover o item de worklist no dcm4chee
 * (código `solicitacaoExame.exclusao_pacs_falhou`) — sinaliza que dá pra forçar.
 */
export function ehFalhaExclusaoPacs(e: unknown): boolean {
  const tipo = (e as { response?: { data?: { type?: string } } })?.response?.data?.type;
  return tipo === 'solicitacaoExame.exclusao_pacs_falhou';
}
