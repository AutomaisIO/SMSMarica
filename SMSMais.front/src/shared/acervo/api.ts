import { http } from '@/shared/api/httpClient';
import type { ItemAcervo } from './tipos';

/** Arquivo baixado do backend (com bearer) para mostrar no visualizador. */
export type ArquivoBaixado = { blob: Blob; mimeType: string; nomeArquivo: string | null };

/**
 * Baixa um arquivo autenticado. Os endpoints de conteúdo exigem o bearer no header, então não dá
 * para pôr a URL direto num <img>/<iframe> — vem como blob e vira blob URL.
 */
export async function baixarArquivo(caminho: string): Promise<ArquivoBaixado> {
  const resp = await http.get<Blob>(caminho, { responseType: 'blob' });
  const tipoCabecalho = String(resp.headers['content-type'] ?? '').split(';')[0].trim();
  const mimeType = tipoCabecalho || resp.data.type || 'application/octet-stream';
  return {
    blob: resp.data.type === mimeType ? resp.data : new Blob([resp.data], { type: mimeType }),
    mimeType,
    nomeArquivo: nomeDoCabecalho(String(resp.headers['content-disposition'] ?? '')),
  };
}

function nomeDoCabecalho(cd: string): string | null {
  const utf = /filename\*=UTF-8''([^;]+)/i.exec(cd);
  if (utf) {
    try {
      return decodeURIComponent(utf[1]);
    } catch {
      /* cai no nome simples */
    }
  }
  const simples = /filename="?([^";]+)"?/i.exec(cd);
  return simples ? simples[1] : null;
}

// ---------------------------------------------------------------- cadastro do paciente

const basePaciente = (pacienteId: string) => `/pacientes/${pacienteId}/documentos`;

export async function listarAcervoPaciente(pacienteId: string): Promise<ItemAcervo[]> {
  const { data } = await http.get<ItemAcervo[]>(basePaciente(pacienteId));
  return data;
}

export function caminhoConteudoPaciente(pacienteId: string, item: Pick<ItemAcervo, 'tipo' | 'id'>): string {
  return `${basePaciente(pacienteId)}/${item.tipo}/${item.id}/conteudo`;
}

export async function enviarDocumentoPaciente(
  pacienteId: string,
  arquivo: File,
  titulo: string,
  descricao: string | null,
): Promise<ItemAcervo> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  form.append('titulo', titulo);
  if (descricao) form.append('descricao', descricao);
  const { data } = await http.post<ItemAcervo>(basePaciente(pacienteId), form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

export async function editarDocumentoPaciente(
  pacienteId: string,
  documentoId: string,
  titulo: string,
  descricao: string | null,
): Promise<ItemAcervo> {
  const { data } = await http.put<ItemAcervo>(`${basePaciente(pacienteId)}/${documentoId}`, { titulo, descricao });
  return data;
}

export async function aceitarDocumentoPaciente(
  pacienteId: string,
  documentoId: string,
  titulo: string,
  descricao: string | null,
): Promise<ItemAcervo> {
  const { data } = await http.post<ItemAcervo>(`${basePaciente(pacienteId)}/${documentoId}/aceitar`, {
    titulo,
    descricao,
  });
  return data;
}

export async function excluirDocumentoPaciente(pacienteId: string, documentoId: string): Promise<void> {
  await http.delete(`${basePaciente(pacienteId)}/${documentoId}`);
}

/** Monta o FormData de um anexo com título e descrição (regulação, SER, SERNIT). */
export function formularioAnexo(arquivo: File, titulo?: string | null, descricao?: string | null): FormData {
  const form = new FormData();
  form.append('arquivo', arquivo);
  if (titulo) form.append('titulo', titulo);
  if (descricao) form.append('descricao', descricao);
  return form;
}

/**
 * Anexos de rascunho do SER/SERNIT (`base` = `/regulacao/ser/rascunhos` ou `/regulacao/sernit/rascunhos`):
 * upload com nome/descrição, conteúdo para o visualizador e "anexar do cadastro" pelo CNS do rascunho.
 */
export function apiAnexosRascunho(base: string) {
  return {
    enviar: async (rascunhoId: string, arquivo: File, titulo: string, descricao: string | null) => {
      await http.post(`${base}/${rascunhoId}/anexos`, formularioAnexo(arquivo, titulo, descricao), {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
    },
    caminhoConteudo: (rascunhoId: string, anexoId: string) => `${base}/${rascunhoId}/anexos/${anexoId}/conteudo`,
    listarAcervo: async (rascunhoId: string) => {
      const { data } = await http.get<ItemAcervo[]>(`${base}/${rascunhoId}/acervo`);
      return data;
    },
    caminhoConteudoAcervo: (rascunhoId: string, item: ItemAcervo) =>
      `${base}/${rascunhoId}/acervo/conteudo?chave=${encodeURIComponent(item.chave)}`,
    anexarDoAcervo: async (rascunhoId: string, chave: string) => {
      await http.post(`${base}/${rascunhoId}/anexos/do-acervo`, { chave });
    },
  };
}
