import { apiBaseAbsoluto, http } from '@/shared/api/httpClient';

export type Midia = {
  id: string;
  nomeArquivo: string;
  mimeType: string;
  tamanhoBytes: number;
  largura: number | null;
  altura: number | null;
  categoria: string | null;
  criadoEm: string;
  /** URL relativa servida pelo backend (ex.: `/midias/{id}`). */
  url: string;
};

/** Envia uma imagem para o store genérico e devolve seus metadados. */
export async function enviarMidia(arquivo: File, categoria?: string): Promise<Midia> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  if (categoria) form.append('categoria', categoria);

  const { data } = await http.post<Midia>('/midias', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

/**
 * Resolve a URL absoluta de uma mídia para usar como `src` de `<img>`. Precisa
 * ser absoluta porque a tag `<img>` é carregada pelo browser apontando para a
 * API (não para a origem do SPA) e sem header de autenticação — por isso o
 * `GET /midias/{id}` é anônimo.
 */
export function urlMidiaAbsoluta(urlOuId: string): string {
  const caminho = urlOuId.startsWith('/midias/')
    ? urlOuId
    : `/midias/${urlOuId}`;
  const base = apiBaseAbsoluto.replace(/\/$/, '');
  return `${base}${caminho}`;
}
