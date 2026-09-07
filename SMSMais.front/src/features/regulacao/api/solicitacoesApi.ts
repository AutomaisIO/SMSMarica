import { http } from '@/shared/api/httpClient';

import type {
  AvaliacaoElegibilidade,
  EscopoNotificacao,
  EventoRegulacao,
  Exigencia,
  FiltroSolicitacoesRegulacao,
  FluxoRegulacao,
  FormularioRegulacao,
  PaginaNotificacoesRegulacao,
  PaginaSolicitacoesRegulacao,
  ExameParaRegras,
  PendenciaEnvio,
  RespostaRegraRegulacao,
  ResumoFilaRegulacao,
  SolicitacaoRegulacao,
} from '../tiposSolicitacao';

const base = '/regulacao/solicitacoes';

export type CriarSolicitacaoPayload = {
  fluxo: FluxoRegulacao;
  procedimentoId: string;
  pacienteId: string;
  /** Obrigatória no NAR (D-9). */
  unidadeEmNomeDeId?: string | null;
  sistemaDestino?: string | null;
  observacoes?: string | null;
};

export type AtualizarSolicitacaoPayload = {
  sistemaDestino?: string | null;
  formulario?: Record<string, unknown> | null;
  observacoes?: string | null;
};

export async function criarSolicitacao(p: CriarSolicitacaoPayload): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(base, p);
  return data;
}

export async function obterSolicitacao(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.get<SolicitacaoRegulacao>(`${base}/${id}`);
  return data;
}

export async function atualizarSolicitacao(
  id: string,
  p: AtualizarSolicitacaoPayload,
): Promise<SolicitacaoRegulacao> {
  const { data } = await http.put<SolicitacaoRegulacao>(`${base}/${id}`, p);
  return data;
}

export async function obterFormularioRegulacao(
  procedimentoId: string,
  fluxo: FluxoRegulacao,
): Promise<FormularioRegulacao> {
  const { data } = await http.get<FormularioRegulacao>(`${base}/formulario`, {
    params: { procedimentoId, fluxo },
  });
  return data;
}

export async function obterPendencias(id: string): Promise<PendenciaEnvio[]> {
  const { data } = await http.get<PendenciaEnvio[]>(`${base}/${id}/pendencias`);
  return data;
}

export async function enviarParaFila(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/enviar-fila`);
  return data;
}

export async function listarExigencias(solicitacaoId: string): Promise<Exigencia[]> {
  const { data } = await http.get<Exigencia[]>(`${base}/${solicitacaoId}/exigencias`);
  return data;
}

export async function anexarArquivo(
  solicitacaoId: string,
  exigenciaId: string,
  arquivo: File,
): Promise<void> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  await http.post(`${base}/${solicitacaoId}/exigencias/${exigenciaId}/arquivos`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
}

export async function removerArquivo(solicitacaoId: string, arquivoId: string): Promise<void> {
  await http.delete(`${base}/${solicitacaoId}/exigencias/arquivos/${arquivoId}`);
}

// ---------------------------------------------------------------- fila (plano 04)

/**
 * A fila. Quem tem só o módulo 47 recebe as solicitações das suas unidades; quem tem o 48 recebe
 * o município inteiro — a decisão é do backend, a tela não escolhe.
 */
export async function listarSolicitacoes(
  filtro: FiltroSolicitacoesRegulacao,
): Promise<PaginaSolicitacoesRegulacao> {
  const { data } = await http.get<PaginaSolicitacoesRegulacao>(base, {
    params: filtro,
    // `status` é uma lista: sem isto o axios manda `status[]=` e o binder do ASP.NET ignora.
    paramsSerializer: { indexes: null },
  });
  return data;
}

export async function obterResumoFila(): Promise<ResumoFilaRegulacao> {
  const { data } = await http.get<ResumoFilaRegulacao>(`${base}/resumo`);
  return data;
}

export async function listarEventos(id: string): Promise<EventoRegulacao[]> {
  const { data } = await http.get<EventoRegulacao[]>(`${base}/${id}/eventos`);
  return data;
}

// ---------------------------------------------------------------- ações do agente (módulo 48)

export async function assumirSolicitacao(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/assumir`);
  return data;
}

export async function devolverSolicitacao(id: string, motivo: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/devolver`, { motivo });
  return data;
}

export async function recusarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`${base}/${id}/recusar`, { motivo });
}

export async function cancelarSolicitacao(id: string, motivo: string): Promise<void> {
  await http.post(`${base}/${id}/cancelar`, { motivo });
}

export async function registrarEnvioSolicitacao(
  id: string,
  payload: { sistema: string; numeroExterno: string; enviadoEm?: string | null },
): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/registrar-envio`, payload);
  return data;
}

export async function confirmarOkInterno(id: string): Promise<SolicitacaoRegulacao> {
  const { data } = await http.post<SolicitacaoRegulacao>(`${base}/${id}/ok-interno`);
  return data;
}

// ---------------------------------------------------------------- notificações (plano 05)

export async function listarNotificacoesRegulacao(
  escopo: EscopoNotificacao,
  soNaoVistas: boolean,
): Promise<PaginaNotificacoesRegulacao> {
  const { data } = await http.get<PaginaNotificacoesRegulacao>('/regulacao/notificacoes', {
    params: { escopo, soNaoVistas, tamanho: 50 },
  });
  return data;
}

export async function obterResumoNotificacoesRegulacao(
  escopo: EscopoNotificacao,
): Promise<{ naoVistas: number }> {
  const { data } = await http.get<{ naoVistas: number }>('/regulacao/notificacoes/resumo', {
    params: { escopo },
  });
  return data;
}

export async function marcarNotificacaoVista(eventoId: string): Promise<void> {
  await http.post(`/regulacao/notificacoes/${eventoId}/vista`);
}

export async function marcarNotificacoesDaSolicitacaoVistas(solicitacaoId: string): Promise<void> {
  await http.post(`/regulacao/notificacoes/solicitacao/${solicitacaoId}/vistas`);
}

// ---------------------------------------------------------------- elegibilidade (plano 03)

export async function obterElegibilidade(id: string): Promise<AvaliacaoElegibilidade> {
  const { data } = await http.get<AvaliacaoElegibilidade>(`${base}/${id}/elegibilidade`);
  return data;
}

export async function responderRegras(
  id: string,
  respostas: Record<string, RespostaRegraRegulacao>,
): Promise<AvaliacaoElegibilidade> {
  const { data } = await http.put<AvaliacaoElegibilidade>(`${base}/${id}/respostas`, { respostas });
  return data;
}

export async function listarExamesInternos(
  solicitacaoId: string,
  exigenciaId: string,
): Promise<ExameParaRegras[]> {
  const { data } = await http.get<ExameParaRegras[]>(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/exames-internos`,
  );
  return data;
}

export async function usarExameInterno(
  solicitacaoId: string,
  exigenciaId: string,
  exameId: string,
  laudoId?: string | null,
): Promise<Exigencia> {
  const { data } = await http.post<Exigencia>(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/usar-exame-interno`,
    { exameId, laudoId },
  );
  return data;
}
