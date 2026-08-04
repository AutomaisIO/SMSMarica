import { cache, imageLoader, init as coreInit } from '@cornerstonejs/core';
import { init as dicomImageLoaderInit, wadors } from '@cornerstonejs/dicom-image-loader';
import {
  init as toolsInit,
  addTool,
  AngleTool,
  ArrowAnnotateTool,
  EllipticalROITool,
  LengthTool,
  MagnifyTool,
  PanTool,
  ProbeTool,
  StackScrollTool,
  WindowLevelTool,
  ZoomTool,
} from '@cornerstonejs/tools';
import { ScaleOverlayXYTool } from '@/features/pacs/lib/scaleOverlayXY';
import { http } from '@/shared/api/httpClient';
import { obterToken } from '@/shared/auth/authStore';
import type { DatasetDicom } from '@/features/pacs/types';

export const RENDERING_ENGINE_ID = 'pacs-rendering-engine';
export const VIEWPORT_ID = 'pacs-stack-viewport';
export const TOOL_GROUP_ID = 'pacs-tool-group';

/** id da viewport do quadrado `i` da grade (layout multi-viewport). */
export function idViewportCelula(i: number): string {
  return `pacs-vp-${i}`;
}

let promessaInit: Promise<void> | null = null;

/** Inicializa core + dicom-image-loader + tools uma única vez (idempotente). */
export function inicializarCornerstone(): Promise<void> {
  if (!promessaInit) {
    promessaInit = (async () => {
      await coreInit();
      // O dicom-image-loader faz XHR direto (sem axios) ao buscar /frames/* via WADO-RS.
      // beforeSend injeta o Bearer atual pra não cair no [Authorize] global da API.
      dicomImageLoaderInit({
        beforeSend: (_xhr, _imageId, defaultHeaders) => {
          const token = obterToken();
          return token ? { ...defaultHeaders, Authorization: `Bearer ${token}` } : defaultHeaders;
        },
      });
      await toolsInit();
      addTool(PanTool);
      addTool(ZoomTool);
      addTool(WindowLevelTool);
      addTool(LengthTool);
      addTool(StackScrollTool);
      addTool(ArrowAnnotateTool);
      addTool(MagnifyTool);
      addTool(EllipticalROITool);
      addTool(AngleTool);
      addTool(ProbeTool);
      addTool(ScaleOverlayXYTool);
    })();
  }
  return promessaInit;
}

/** Raiz WADO-RS exposta pelo proxy do backend (.../pacs/rs). */
export function wadoRsRoot(): string {
  const base = (http.defaults.baseURL ?? '/api').replace(/\/+$/, '');
  return `${base}/pacs/rs`;
}

/**
 * Versão do encoding dos frames. Entra na URL (`?ev=N`) para versionar o cache
 * HTTP imutável do browser: ao trocar o codec/encoding no backend (ex.: JPEG-LS
 * → JPEG 2000), basta incrementar aqui que TODAS as URLs de frame mudam e o
 * browser rebaixa fresco — sem depender de hard-reload manual. O backend remove
 * esse parâmetro antes de encaminhar ao dcm4chee (que não o conhece).
 * v2: migração JPEG-LS → JPEG 2000 Lossless (JPEG-LS quebrava mamografia 12-bit).
 * v3: frames YBR planar (US Mindray DC-28) passam a ser servidos CRUS — o transcode
 *     reordenava os samples e o viewer exibia listras; expurga os J2K corrompidos
 *     dos caches (browser e disco do proxy, já que ?ev entra na chave).
 */
const VERSAO_ENCODING = 3;

/** imageId WADO-RS para uma instância (frame único por padrão). */
export function construirImageId(
  studyUID: string,
  seriesUID: string,
  sopUID: string,
  frame = 1,
): string {
  return `wadors:${wadoRsRoot()}/studies/${studyUID}/series/${seriesUID}/instances/${sopUID}/frames/${frame}?ev=${VERSAO_ENCODING}`;
}

/**
 * Deriva o imageId que pede o frame CRU (sentinela `?semCompressao=1`): o proxy
 * serve os pixels sem passar pela compressão JPEG-LS. Usado ao "recriar" uma
 * imagem cuja variante comprimida ficou ilegível (abre em branco). A URL nova
 * também fura o cache HTTP imutável do browser. Idempotente.
 */
export function imageIdSemCompressao(imageId: string): string {
  if (imageId.includes('semCompressao=1')) return imageId;
  return imageId + (imageId.includes('?') ? '&' : '?') + 'semCompressao=1';
}

/** Registra os metadados da instância para que o StackViewport consiga renderizar. */
export function registrarMetadados(imageId: string, metadata: DatasetDicom): void {
  wadors.metaDataManager.add(imageId, metadata as never);
}

/**
 * Descarta a imagem do cache em memória do Cornerstone. Após recriar o cache no
 * servidor, isto força o próximo `setStack`/`loadAndCacheImage` a rebaixar os
 * pixels do proxy em vez de reusar a cópia (possivelmente corrompida) em RAM.
 */
export function descartarImagemDoCache(imageId: string): void {
  try {
    cache.removeImageLoadObject(imageId);
  } catch {
    // Imagem não estava em cache (nunca carregada nesta viewport): nada a fazer.
  }
}

export type ProgressoPrefetch = { carregadas: number; total: number };

/**
 * Faz prefetch das imagens em paralelo controlado, populando o cache do
 * Cornerstone. Subsequentes `setStack` com esses imageIds resolvem instantâneo.
 * Aborta cedo se `signal.aborted` — porém não cancela o HTTP em voo (Cornerstone
 * 4.x não expõe AbortSignal no loadAndCacheImage), só evita o próximo download.
 */
export async function prefetchImagens(
  imageIds: string[],
  opts: {
    concurrencia?: number;
    onProgress?: (p: ProgressoPrefetch) => void;
    signal?: AbortSignal;
  } = {},
): Promise<void> {
  const conc = opts.concurrencia ?? 4;
  const total = imageIds.length;
  if (total === 0) return;
  let proximo = 0;
  let carregadas = 0;

  async function worker() {
    while (true) {
      if (opts.signal?.aborted) return;
      const idx = proximo++;
      if (idx >= total) return;
      try {
        await imageLoader.loadAndCacheImage(imageIds[idx]);
      } catch {
        // Imagem com erro não trava o prefetch das demais.
      }
      if (opts.signal?.aborted) return;
      carregadas++;
      opts.onProgress?.({ carregadas, total });
    }
  }

  await Promise.all(Array.from({ length: Math.min(conc, total) }, worker));
}
