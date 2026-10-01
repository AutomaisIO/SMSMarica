import { http } from '@/shared/api/httpClient';
import { formularioAnexo } from '@/shared/acervo/api';
import type { ItemAcervo } from '@/shared/acervo/tipos';

/**
 * Anexos da solicitação ligados ao cadastro do paciente: título e descrição no upload (o arquivo
 * também fica no cadastro), visualizar o que já foi anexado e "anexar do cadastro" sem novo upload.
 */
const base = '/regulacao/solicitacoes';

export async function anexarComTitulo(
  solicitacaoId: string,
  exigenciaId: string,
  arquivo: File,
  titulo: string | null,
  descricao: string | null,
): Promise<void> {
  await http.post(
    `${base}/${solicitacaoId}/exigencias/${exigenciaId}/arquivos`,
    formularioAnexo(arquivo, titulo, descricao),
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );
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
