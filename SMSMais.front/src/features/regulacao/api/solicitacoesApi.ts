import { http } from '@/shared/api/httpClient';

import type {
  Exigencia,
  FluxoRegulacao,
  FormularioRegulacao,
  PendenciaEnvio,
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
