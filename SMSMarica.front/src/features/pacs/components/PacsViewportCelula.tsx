import { useEffect, useRef, useState } from 'react';
import {
  Enums,
  EVENTS,
  metaData,
  type RenderingEngine,
  type Types,
} from '@cornerstonejs/core';
import { ToolGroupManager } from '@cornerstonejs/tools';
import { Loader2 } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { ScaleOverlayXYTool } from '@/features/pacs/lib/scaleOverlayXY';
import { RENDERING_ENGINE_ID, TOOL_GROUP_ID } from '@/features/pacs/lib/cornerstone';
import {
  fontePixelSpacing,
  formatarSpacing,
  labelFonte,
  type FontePixelSpacing,
} from '@/features/pacs/lib/dicomJson';

const { ViewportType } = Enums;

type Props = {
  engine: RenderingEngine | null;
  viewportId: string;
  imageId: string | null;
  focado: boolean;
  aoFocar: () => void;
  /** Chamado quando a imagem desta viewport muda (STACK_NEW_IMAGE). */
  aoNovaImagem?: () => void;
  /** Loading no nível do estudo (metadados ainda carregando). */
  carregando?: boolean;
};

type InfoFooter = {
  zoom: number;
  rowPixelSpacing: number | null;
  columnPixelSpacing: number | null;
  fonte: FontePixelSpacing;
};

/**
 * Um quadrado da grade = uma StackViewport do Cornerstone. Registra seu próprio
 * elemento na RenderingEngine compartilhada e no ToolGroup, monta a imagem que
 * recebe (stack de 1) e exibe rodapé/overlay próprios. A borda de foco é só
 * visual — a interação de mouse já é roteada pelo Cornerstone para a viewport
 * sob o cursor; o foco governa quais botões (girar/flip/etc.) agem aqui.
 */
export function PacsViewportCelula({
  engine,
  viewportId,
  imageId,
  focado,
  aoFocar,
  aoNovaImagem,
  carregando,
}: Props) {
  const elementoRef = useRef<HTMLDivElement>(null);
  const [pronto, setPronto] = useState(false);
  const [montando, setMontando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [info, setInfo] = useState<InfoFooter | null>(null);

  // Registra a viewport na engine + tool group; desfaz ao desmontar (mudança de
  // layout). A engine vive no container; aqui só anexamos/desanexamos.
  useEffect(() => {
    const elemento = elementoRef.current;
    if (!engine || !elemento) return;

    engine.enableElement({
      viewportId,
      type: ViewportType.STACK,
      element: elemento,
    });
    ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.addViewport(viewportId, RENDERING_ENGINE_ID);
    setPronto(true);

    return () => {
      setPronto(false);
      ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.removeViewports(RENDERING_ENGINE_ID, viewportId);
      try {
        engine.disableElement(viewportId);
      } catch {
        // engine já destruído (desmontagem do container) — ignorar.
      }
    };
  }, [engine, viewportId]);

  // Monta a imagem (stack de 1 elemento) quando ela muda.
  useEffect(() => {
    if (!pronto || !engine) return;
    if (!imageId) {
      setInfo(null);
      return;
    }
    const vp = engine.getViewport(viewportId) as Types.IStackViewport | undefined;
    if (!vp) return;

    setErro(null);
    setMontando(true);
    let cancelado = false;
    vp.setStack([imageId], 0)
      .then(() => {
        if (cancelado) return;
        vp.resetCamera();
        vp.render();
        // Re-habilita a régua mm agora que há imagem (o tool captura os cantos
        // da imagem em setToolEnabled — sem imagem nasce com pontos vazios).
        ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.setToolEnabled(ScaleOverlayXYTool.toolName);
      })
      .catch((e) => {
        if (cancelado) return;
        setErro(e instanceof Error ? e.message : 'Falha ao carregar a imagem.');
      })
      .finally(() => {
        if (cancelado) return;
        setMontando(false);
      });

    return () => {
      cancelado = true;
    };
  }, [pronto, engine, viewportId, imageId]);

  // Rodapé (zoom + mm/px + fonte) atualizado nos eventos da própria viewport.
  useEffect(() => {
    if (!pronto || !engine) return;
    const elemento = elementoRef.current;
    if (!elemento) return;

    function atualizar() {
      const vp = engine!.getViewport(viewportId) as Types.IStackViewport | undefined;
      const idAtual = vp?.getCurrentImageId?.();
      if (!vp || !idAtual) {
        setInfo(null);
        return;
      }
      const plano = metaData.get('imagePlaneModule', idAtual) as
        | { rowPixelSpacing?: number; columnPixelSpacing?: number }
        | undefined;
      setInfo({
        zoom: vp.getZoom(),
        rowPixelSpacing: plano?.rowPixelSpacing ?? null,
        columnPixelSpacing: plano?.columnPixelSpacing ?? null,
        fonte: fontePixelSpacing(idAtual),
      });
    }

    elemento.addEventListener(EVENTS.CAMERA_MODIFIED, atualizar);
    elemento.addEventListener(EVENTS.STACK_NEW_IMAGE, atualizar);
    elemento.addEventListener(EVENTS.IMAGE_RENDERED, atualizar);
    return () => {
      elemento.removeEventListener(EVENTS.CAMERA_MODIFIED, atualizar);
      elemento.removeEventListener(EVENTS.STACK_NEW_IMAGE, atualizar);
      elemento.removeEventListener(EVENTS.IMAGE_RENDERED, atualizar);
    };
  }, [pronto, engine, viewportId]);

  // Notifica o container quando a imagem desta viewport troca (para limpar a
  // seleção global de anotações). STACK_NEW_IMAGE só dispara no elemento.
  useEffect(() => {
    if (!pronto || !aoNovaImagem) return;
    const elemento = elementoRef.current;
    if (!elemento) return;
    const handler = () => aoNovaImagem();
    elemento.addEventListener(EVENTS.STACK_NEW_IMAGE, handler);
    return () => elemento.removeEventListener(EVENTS.STACK_NEW_IMAGE, handler);
  }, [pronto, aoNovaImagem]);

  const mostrarLoading = Boolean(imageId) && (montando || carregando);

  return (
    <div
      className={cn(
        'relative bg-black',
        focado ? 'ring-2 ring-primary-500 ring-inset' : 'ring-1 ring-gray-800 ring-inset',
      )}
      onPointerDown={aoFocar}
    >
      <div
        ref={elementoRef}
        className="absolute inset-0"
        onContextMenu={(e) => e.preventDefault()}
      />

      {!imageId ? (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-xs text-gray-600">
          {focado ? 'Clique numa imagem' : 'Vazio'}
        </div>
      ) : null}

      {mostrarLoading ? (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center gap-2 bg-black/70 text-sm text-gray-300">
          <Loader2 className="h-5 w-5 animate-spin" />
          Carregando...
        </div>
      ) : null}

      {erro ? (
        <div className="absolute inset-x-0 bottom-0 bg-red-900/80 px-2 py-1 text-xs text-red-100">
          {erro}
        </div>
      ) : null}

      {info && imageId && !montando ? (
        <div className="pointer-events-none absolute bottom-1 left-2 select-none font-mono text-[10px] leading-tight text-gray-400 mix-blend-screen">
          <div>Zoom: {(info.zoom * 100).toFixed(0)}%</div>
          {info.rowPixelSpacing != null ? (
            <div>{formatarSpacing(info.rowPixelSpacing, info.columnPixelSpacing)}</div>
          ) : null}
          <div className={info.fonte === 'estimado' ? 'text-amber-400' : ''}>
            {labelFonte(info.fonte)}
          </div>
        </div>
      ) : null}
    </div>
  );
}
