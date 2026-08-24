import { annotation as annotationManager } from '@cornerstonejs/tools';
import type { PayloadAnotacoes } from '@/features/pacs/types';

type AnnotationCornerstone = {
  annotationUID?: string;
  metadata?: { FrameOfReferenceUID?: string };
  [k: string]: unknown;
};

/**
 * Snapshot completo do estado de annotations do Cornerstone. Salvamos o array
 * inteiro retornado por `getAllAnnotations()` — é o formato que o próprio
 * `addAnnotation` aceita de volta na restauração.
 */
export function serializarAnotacoes(): PayloadAnotacoes {
  const todas = annotationManager.state.getAllAnnotations() as AnnotationCornerstone[];
  // Estrutura como objeto pra abrir margem para metadados futuros sem migration de payload.
  return {
    versao: 1,
    annotations: todas,
  };
}

/**
 * Limpa todas as annotations atuais e re-hidrata a partir do payload salvo.
 * Espera o formato emitido por `serializarAnotacoes` — também tolera o array cru
 * (compatibilidade caso alguém grave direto o retorno de `getAllAnnotations`).
 */
export function restaurarAnotacoes(payload: PayloadAnotacoes): void {
  removerTodasAnotacoes();

  const lista = extrairLista(payload);
  for (const anot of lista) {
    // Imagens 2D (CR/DX/MG) tipicamente não carregam FrameOfReferenceUID. O
    // `addAnnotation` do Cornerstone ignora o 2º arg quando não é HTMLDivElement
    // e cai em `groupKey || metadata.FrameOfReferenceUID` — então passar string
    // vazia é seguro, e o manager usa o FoR UID que está no próprio metadata
    // (mesmo que vazio), garantindo que o groupKey de leitura no render bata
    // com o de gravação. Pular aqui descarta toda anotação de radiografia.
    const frame = anot?.metadata?.FrameOfReferenceUID ?? '';
    annotationManager.state.addAnnotation(
      anot as unknown as Parameters<typeof annotationManager.state.addAnnotation>[0],
      frame,
    );
  }
}

export function removerTodasAnotacoes(): void {
  const todas = annotationManager.state.getAllAnnotations() as AnnotationCornerstone[];
  for (const a of todas) {
    if (a.annotationUID) annotationManager.state.removeAnnotation(a.annotationUID);
  }
  annotationManager.selection.deselectAnnotation();
}

function extrairLista(payload: PayloadAnotacoes): AnnotationCornerstone[] {
  if (Array.isArray(payload)) return payload as AnnotationCornerstone[];
  if (payload && typeof payload === 'object') {
    const lista = (payload as { annotations?: unknown }).annotations;
    if (Array.isArray(lista)) return lista as AnnotationCornerstone[];
  }
  return [];
}
