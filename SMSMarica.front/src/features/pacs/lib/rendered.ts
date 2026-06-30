import { useEffect, useState } from 'react';
import { http } from '@/shared/api/httpClient';

/**
 * Deriva o caminho WADO-RS <c>/rendered</c> (JPEG já decodificado/janelado pelo
 * servidor) a partir do imageId wadors de um frame. O <c>viewport</c> opcional
 * pede ao dcm4chee uma versão reduzida (thumb/preview) — barata e instantânea
 * (thumb 160px ~2,5KB, preview 1024px ~56KB) versus a imagem diagnóstica crua
 * (~53MB). NÃO serve para laudar (é 8-bit lossy), só para exibição rápida.
 *
 * Retorna um caminho relativo à baseURL do axios (ex.: <c>/pacs/rs/studies/.../rendered</c>),
 * para o interceptor do http injetar o Bearer normalmente.
 */
export function urlRendered(imageId: string, viewport?: number): string | null {
  const m = imageId.match(/\/(studies\/[^?]+?\/instances\/[^/?]+)\/frames\/\d+/);
  if (!m) return null;
  const q = viewport ? `?viewport=${viewport},${viewport}` : '';
  return `/pacs/rs/${m[1]}/rendered${q}`;
}

/**
 * Baixa a imagem <c>/rendered</c> autenticada (via axios, que injeta o Bearer)
 * como blob e devolve um object URL pronto para <img>. Revoga ao trocar de
 * imagem ou desmontar. Aproveita o cache do proxy + do browser.
 */
export function useImagemRendered(imageId: string | null, viewport?: number): string | null {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!imageId) {
      setUrl(null);
      return;
    }
    const caminho = urlRendered(imageId, viewport);
    if (!caminho) {
      setUrl(null);
      return;
    }

    let cancelado = false;
    let objectUrl: string | null = null;
    http
      .get(caminho, { responseType: 'blob' })
      .then((r) => {
        if (cancelado) return;
        objectUrl = URL.createObjectURL(r.data as Blob);
        setUrl(objectUrl);
      })
      .catch(() => {
        if (!cancelado) setUrl(null);
      });

    return () => {
      cancelado = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [imageId, viewport]);

  return url;
}
