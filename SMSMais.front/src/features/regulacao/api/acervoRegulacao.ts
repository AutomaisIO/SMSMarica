import { http } from '@/shared/api/httpClient';
import { formularioAnexo } from '@/shared/acervo/api';
import type { ArquivoPreparado, CompactacaoNaEntrada } from '@/shared/acervo/PreviaCompactacao';
import type { ItemAcervo } from '@/shared/acervo/tipos';

/**
 * Anexos da solicitação ligados ao cadastro do paciente: upload com prévia da compactação (o arquivo
 * também fica no cadastro), visualizar o que já foi anexado e "anexar do cadastro" sem novo upload.
 */
const base = '/regulacao/solicitacoes';

/**
 * Primeiro passo do anexo: o servidor compacta e guarda o resultado só em memória, por 30 minutos.
 * Nada entra na solicitação até {@link aceitarAnexoPreparado}.
 */
export async function prepararAnexo(
  solicitacaoId: string,
  exigenciaId: string,
  arquivo: File,
): Promise<ArquivoPreparado> {
  const { data } = await http.post<ArquivoPreparado>(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/arquivos/preparar`,
    formularioAnexo(arquivo),
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
  return data;
}

export function caminhoAnexoPreparado(solicitacaoId: string, token: string): string {
  return `${base}/${solicitacaoId}/exigencias/preparados/${token}/conteudo`;
}

/** Quem anexou conferiu a prévia: o arquivo compactado entra na caixinha (e no cadastro do paciente). */
export async function aceitarAnexoPreparado(
  solicitacaoId: string,
  token: string,
  titulo: string | null,
  descricao: string | null,
): Promise<void> {
  await http.post(`${base}/${solicitacaoId}/exigencias/preparados/${token}/aceitar`, { titulo, descricao });
}

/** Recusou a prévia: o servidor esquece o arquivo. Descartar o que já expirou não é erro. */
export async function descartarAnexoPreparado(solicitacaoId: string, token: string): Promise<void> {
  await http.delete(`${base}/${solicitacaoId}/exigencias/preparados/${token}`);
}

/** A compactação na entrada de uma caixinha, para o `UploadAnexo`. */
export function compactacaoDaRegulacao(
  solicitacaoId: string,
  exigenciaId: string,
  depoisDeAnexar?: () => Promise<unknown>,
): CompactacaoNaEntrada {
  return {
    preparar: (arquivo) => prepararAnexo(solicitacaoId, exigenciaId, arquivo),
    caminhoConteudo: (p) => caminhoAnexoPreparado(solicitacaoId, p.token),
    aceitar: async (p, titulo, descricao) => {
      await aceitarAnexoPreparado(solicitacaoId, p.token, titulo, descricao);
      await depoisDeAnexar?.();
    },
    descartar: (p) => descartarAnexoPreparado(solicitacaoId, p.token),
  };
}

export function caminhoArquivoExigencia(solicitacaoId: string, arquivoId: string): string {
  return `${base}/${solicitacaoId}/exigencias/arquivos/${arquivoId}/conteudo`;
}

export async function listarAcervoDaSolicitacao(solicitacaoId: string): Promise<ItemAcervo[]> {
  const { data } = await http.get<ItemAcervo[]>(`${base}/${solicitacaoId}/acervo`);
  return data;
}

export function caminhoConteudoAcervo(solicitacaoId: string, item: ItemAcervo): string {
  return `${base}/${solicitacaoId}/acervo/conteudo?chave=${encodeURIComponent(item.chave)}`;
}

export async function anexarDoAcervo(solicitacaoId: string, exigenciaId: string, chave: string): Promise<void> {
  await http.post(`${base}/${solicitacaoId}/exigencias/${exigenciaId}/do-acervo`, { chave });
}
