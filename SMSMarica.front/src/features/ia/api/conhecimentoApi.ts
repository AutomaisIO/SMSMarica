import { http } from '@/shared/api/httpClient';
import type {
  DocumentoConhecimento,
  DocumentoConhecimentoDetalhe,
  ExtracaoModeloResultado,
  TokenAgenteGerado,
} from '@/features/ia/types';

// ---- Repositório de documentos (.md) por base ----

export async function listarDocumentos(fonteId: string): Promise<DocumentoConhecimento[]> {
  const { data } = await http.get<DocumentoConhecimento[]>(`/ia/conhecimento/${fonteId}/documentos`);
  return data;
}

export async function obterDocumento(
  fonteId: string,
  docId: string,
): Promise<DocumentoConhecimentoDetalhe> {
  const { data } = await http.get<DocumentoConhecimentoDetalhe>(
    `/ia/conhecimento/${fonteId}/documentos/${docId}`,
  );
  return data;
}

export async function criarDocumento(
  fonteId: string,
  payload: { caminho: string; conteudo: string },
): Promise<DocumentoConhecimentoDetalhe> {
  const { data } = await http.post<DocumentoConhecimentoDetalhe>(
    `/ia/conhecimento/${fonteId}/documentos`,
    payload,
  );
  return data;
}

export async function atualizarDocumento(
  fonteId: string,
  docId: string,
  payload: { caminho: string; conteudo: string },
): Promise<DocumentoConhecimentoDetalhe> {
  const { data } = await http.put<DocumentoConhecimentoDetalhe>(
    `/ia/conhecimento/${fonteId}/documentos/${docId}`,
    payload,
  );
  return data;
}

export async function removerDocumento(fonteId: string, docId: string): Promise<void> {
  await http.delete(`/ia/conhecimento/${fonteId}/documentos/${docId}`);
}

/** Levanta a estrutura da base (tabelas + relacionamentos) e gera um doc por tabela. */
export async function extrairModelo(
  fonteId: string,
  maxTabelas = 2000,
): Promise<ExtracaoModeloResultado> {
  const { data } = await http.post<ExtracaoModeloResultado>(
    `/ia/conhecimento/${fonteId}/extrair-modelo`,
    { maxTabelas },
  );
  return data;
}

// ---- Token do agente proxy (mostrado uma vez) ----

export async function gerarTokenAgente(fonteId: string): Promise<TokenAgenteGerado> {
  const { data } = await http.post<TokenAgenteGerado>(
    `/ia/configuracao/fontes/${fonteId}/token-agente`,
    {},
  );
  return data;
}
