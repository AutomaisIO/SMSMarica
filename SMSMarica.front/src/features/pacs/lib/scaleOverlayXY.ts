import { vec3 } from 'gl-matrix';
import {
  getEnabledElementByIds,
  getRenderingEngines,
  utilities as csUtils,
  type Types,
} from '@cornerstonejs/core';
import {
  ScaleOverlayTool,
  ToolGroupManager,
  drawing,
  annotation as annotationManager,
} from '@cornerstonejs/tools';

const { drawLine: drawLineSvg, drawTextBox: drawTextBoxSvg } = drawing;

type Location = 'bottom' | 'left';

/**
 * Variante do ScaleOverlayTool que desenha duas réguas (horizontal no rodapé
 * e vertical na lateral esquerda) a partir da MESMA annotation por viewport.
 *
 * Motivação:
 *  - O `ScaleOverlayTool` original desenha uma régua só por instância
 *    (`scaleLocation`), então não dá pra ter X e Y simultâneos só registrando
 *    a mesma classe duas vezes (`static toolName` colide).
 *  - O `_init` original mantém um `viewportsWithAnnotations` em escopo de
 *    módulo: se o tool é habilitado antes da stack montar, ele cria a
 *    annotation com `points=[]` (porque `getViewportImageCornersInWorld`
 *    devolve `[]` sem imageData) e nunca mais recria — só atualiza via
 *    `editData` no caminho de `onCameraModified`, que nem sempre é confiável
 *    em remounts. Aqui sobrescrevemos `_init` pra: (a) abortar se ainda não
 *    há imagem, e (b) sempre reescrever os pontos da annotation existente
 *    com os cantos atuais — assim cada `setToolEnabled` re-sincroniza.
 *
 * Mantemos compatibilidade com tudo que o ScaleOverlayTool faz: o tool é
 * `Enabled` (sem bindings), responde a CAMERA_MODIFIED, e o magnify-viewport
 * herda o desenho ao entrar no nosso ToolGroup.
 */

/* eslint-disable @typescript-eslint/no-explicit-any */
type ScaleOverlayInternal = ScaleOverlayTool & {
  editData: { renderingEngine: any; viewport: Types.IViewport; annotation: any } | null;
  configuration: { viewportId: string; scaleLocation: string };
  toolGroupId: string;
  getStyle: (name: string, specifier: any, annotation: any) => any;
  computeScaleBounds: (
    canvasSize: { width: number; height: number },
    h: number,
    v: number,
    loc: string,
  ) => { width: number; height: number };
  computeScaleSize: (worldWidth: number, worldHeight: number, loc: string) => number;
  computeWorldScaleCoordinates: (scaleSize: number, loc: string, pointSet: any[]) => any[];
  computeCanvasScaleCoordinates: (
    canvasSize: any,
    canvasCoords: any,
    vBounds: any,
    hBounds: any,
    loc: string,
  ) => any[];
  computeEndScaleTicks: (
    canvasCoords: any[],
    loc: string,
  ) => { endTick1: number[][]; endTick2: number[][] };
  computeInnerScaleTicks: (
    scaleSize: number,
    loc: string,
    annotationUID: string,
    leftTick: any,
    rightTick: any,
  ) => { tickIds: string[]; tickUIDs: string[]; tickCoordinates: any[] };
  _getTextLines: (scaleSize: number) => string[] | undefined;
};

export class ScaleOverlayXYTool extends ScaleOverlayTool {
  static toolName = 'ScaleOverlayXY';

  constructor(toolProps: any = {}, defaultToolProps: any = undefined) {
    super(
      toolProps,
      defaultToolProps ?? {
        configuration: { viewportId: '', scaleLocation: 'bottom' },
      },
    );

    // Sobrescrevemos `_init` pra ser idempotente e tolerante a viewport sem
    // imagem. Reuso os helpers do pai via `this`.
    const self = this as unknown as ScaleOverlayInternal & { _init: () => void };
    self._init = () => {
      const renderingEngines = getRenderingEngines();
      const renderingEngine = renderingEngines?.[0];
      if (!renderingEngine) return;

      const group = ToolGroupManager.getToolGroup(self.toolGroupId);
      if (!group) return;
      const viewportInfos = group.viewportsInfo;
      if (!viewportInfos || viewportInfos.length === 0) return;

      const enabledElements = viewportInfos
        .map((e: any) => getEnabledElementByIds(e.viewportId, e.renderingEngineId))
        .filter((e: any): e is NonNullable<typeof e> => !!e);
      if (enabledElements.length === 0) return;

      for (const element of enabledElements) {
        const { viewport, FrameOfReferenceUID } = element;
        // Sem image data ainda → cantos vazios. Pula este viewport; o
        // próximo CAMERA_MODIFIED (que dispara após setStack/render) chamará
        // _init de novo e desta vez teremos cantos válidos.
        const corners = csUtils.getViewportImageCornersInWorld(viewport);
        if (!corners || corners.length === 0) continue;

        const { viewUp, viewPlaneNormal } = viewport.getCamera();
        const existing = annotationManager.state
          .getAnnotations(self.getToolName(), viewport.element as HTMLDivElement)
          .filter((a: any) => a.data?.viewportId === viewport.id)[0];

        if (existing) {
          // Reusa: só atualiza pontos pra refletir o estado atual.
          if (existing.data?.handles) {
            existing.data.handles.points = corners;
          }
        } else {
          annotationManager.state.addAnnotation(
            {
              metadata: {
                toolName: self.getToolName(),
                viewPlaneNormal: [...viewPlaneNormal],
                viewUp: [...viewUp],
                FrameOfReferenceUID,
                referencedImageId: null,
              },
              data: {
                handles: { points: corners },
                viewportId: viewport.id,
              },
            } as any,
            viewport.element as HTMLDivElement,
          );
        }
      }

      // editData precisa estar setado pra `renderAnnotation` não dar early-return.
      const fallbackViewport = enabledElements[0].viewport;
      self.editData = {
        viewport: fallbackViewport,
        renderingEngine,
        annotation: undefined as any,
      };
    };
  }

  // Desenha bottom (X) e left (Y) a partir da MESMA annotation. IDs de SVG
  // são prefixados pelo eixo pra não colidirem dentro do svgDrawingHelper.
  renderAnnotation(
    enabledElement: Types.IEnabledElement,
    svgDrawingHelper: any,
  ): boolean {
    const self = this as unknown as ScaleOverlayInternal;
    if (!self.editData || !self.editData.viewport) return false;

    const { viewport } = enabledElement;
    const annotation = annotationManager.state
      .getAnnotations(self.getToolName(), viewport.element as HTMLDivElement)
      .filter((a: any) => a.data?.viewportId === viewport.id)[0];
    if (!annotation) return false;

    const points = annotation.data?.handles?.points;
    if (!points || points.length < 4) return false;

    this._drawAxis(enabledElement, svgDrawingHelper, annotation, 'bottom');
    this._drawAxis(enabledElement, svgDrawingHelper, annotation, 'left');
    return false;
  }

  private _drawAxis(
    enabledElement: Types.IEnabledElement,
    svgDrawingHelper: any,
    annotation: any,
    location: Location,
  ): void {
    const self = this as unknown as ScaleOverlayInternal;
    const { viewport } = enabledElement;
    const canvas = (viewport as any).canvas as HTMLCanvasElement;
    if (!canvas) return;

    const ratio = window.devicePixelRatio || 1;
    const canvasSize = {
      width: canvas.width / ratio,
      height: canvas.height / ratio,
    };

    const topLeft = annotation.data.handles.points[0];
    const topRight = annotation.data.handles.points[1];
    const bottomLeft = annotation.data.handles.points[2];
    const bottomRight = annotation.data.handles.points[3];
    const pointSet = [topLeft, bottomLeft, topRight, bottomRight];

    const worldWidth = vec3.distance(bottomLeft, bottomRight);
    const worldHeight = vec3.distance(topLeft, bottomLeft);
    if (!Number.isFinite(worldWidth) || !Number.isFinite(worldHeight)) return;
    if (worldWidth <= 0 || worldHeight <= 0) return;

    const hBounds = self.computeScaleBounds(canvasSize, 0.05, 0.05, location);
    const vBounds = self.computeScaleBounds(canvasSize, 0.05, 0.05, location);
    const scaleSize = self.computeScaleSize(worldWidth, worldHeight, location);
    if (!scaleSize) return;

    const worldCoords = self.computeWorldScaleCoordinates(scaleSize, location, pointSet);
    const canvasCoords = worldCoords.map((w: any) => (viewport as any).worldToCanvas(w));
    const scaleCanvasCoords = self.computeCanvasScaleCoordinates(
      canvasSize,
      canvasCoords,
      vBounds,
      hBounds,
      location,
    );
    const scaleTicks = self.computeEndScaleTicks(scaleCanvasCoords, location);

    const { annotationUID } = annotation;
    const styleSpecifier: any = {
      toolGroupId: self.toolGroupId,
      toolName: self.getToolName(),
      viewportId: viewport.id,
      annotationUID,
    };
    const lineWidth = self.getStyle('lineWidth', styleSpecifier, annotation);
    const lineDash = self.getStyle('lineDash', styleSpecifier, annotation);
    const color = self.getStyle('color', styleSpecifier, annotation);
    const shadow = self.getStyle('shadow', styleSpecifier, annotation);

    const prefix = location === 'bottom' ? 'x' : 'y';

    drawLineSvg(
      svgDrawingHelper,
      annotationUID,
      `${prefix}-line`,
      scaleCanvasCoords[0],
      scaleCanvasCoords[1],
      { color, width: lineWidth, lineDash, shadow },
      `${annotationUID}-${prefix}-line`,
    );
    drawLineSvg(
      svgDrawingHelper,
      annotationUID,
      `${prefix}-tickA`,
      scaleTicks.endTick1[0] as Types.Point2,
      scaleTicks.endTick1[1] as Types.Point2,
      { color, width: lineWidth, lineDash, shadow },
      `${annotationUID}-${prefix}-tickA`,
    );
    drawLineSvg(
      svgDrawingHelper,
      annotationUID,
      `${prefix}-tickB`,
      scaleTicks.endTick2[0] as Types.Point2,
      scaleTicks.endTick2[1] as Types.Point2,
      { color, width: lineWidth, lineDash, shadow },
      `${annotationUID}-${prefix}-tickB`,
    );

    const inner = self.computeInnerScaleTicks(
      scaleSize,
      location,
      annotationUID,
      scaleTicks.endTick1,
      scaleTicks.endTick2,
    );
    for (let i = 0; i < inner.tickUIDs.length; i++) {
      drawLineSvg(
        svgDrawingHelper,
        annotationUID,
        `${prefix}-${inner.tickUIDs[i]}`,
        inner.tickCoordinates[i][0],
        inner.tickCoordinates[i][1],
        { color, width: lineWidth, lineDash, shadow },
        `${annotationUID}-${prefix}-${inner.tickUIDs[i]}`,
      );
    }

    // Texto: posiciona o rótulo perto do início da régua, deslocando pra
    // não cair sobre os ticks (offsets calibrados pra cada eixo).
    const textOffset: Record<Location, [number, number]> = {
      bottom: [-10, -42],
      left: [-40, -20],
    };
    const textCoords: [number, number] = [
      scaleCanvasCoords[0][0] + textOffset[location][0],
      scaleCanvasCoords[0][1] + textOffset[location][1],
    ];
    const textLines = self._getTextLines(scaleSize);
    if (textLines && textLines.length > 0) {
      drawTextBoxSvg(
        svgDrawingHelper,
        annotationUID,
        `${prefix}-text`,
        textLines,
        textCoords,
        {
          fontFamily: 'Helvetica Neue, Helvetica, Arial, sans-serif',
          fontSize: '14px',
          lineDash: '2,3',
          lineWidth: '1',
          shadow: true,
          color,
        },
      );
    }
  }
}
