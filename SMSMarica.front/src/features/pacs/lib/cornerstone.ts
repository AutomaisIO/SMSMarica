import { init as coreInit } from '@cornerstonejs/core';
import { init as dicomImageLoaderInit, wadors } from '@cornerstonejs/dicom-image-loader';
import {
  init as toolsInit,
  addTool,
  ArrowAnnotateTool,
  PanTool,
  ZoomTool,
  WindowLevelTool,
  LengthTool,
  StackScrollTool,
} from '@cornerstonejs/tools';
import { http } from '@/shared/api/httpClient';
import { obterToken } from '@/shared/auth/authStore';
import type { DatasetDicom } from '@/features/pacs/types';

export const RENDERING_ENGINE_ID = 'pacs-rendering-engine';
export const VIEWPORT_ID = 'pacs-stack-viewport';
export const TOOL_GROUP_ID = 'pacs-tool-group';

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
    })();
  }
  return promessaInit;
}

/** Raiz WADO-RS exposta pelo proxy do backend (.../pacs/rs). */
export function wadoRsRoot(): string {
  const base = (http.defaults.baseURL ?? '/api').replace(/\/+$/, '');
  return `${base}/pacs/rs`;
}

/** imageId WADO-RS para uma instância (frame único por padrão). */
export function construirImageId(
  studyUID: string,
  seriesUID: string,
  sopUID: string,
  frame = 1,
): string {
  return `wadors:${wadoRsRoot()}/studies/${studyUID}/series/${seriesUID}/instances/${sopUID}/frames/${frame}`;
}

/** Registra os metadados da instância para que o StackViewport consiga renderizar. */
export function registrarMetadados(imageId: string, metadata: DatasetDicom): void {
  wadors.metaDataManager.add(imageId, metadata as never);
}
