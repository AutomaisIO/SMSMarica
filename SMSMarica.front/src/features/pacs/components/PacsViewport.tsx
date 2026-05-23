import { useEffect, useRef, useState } from 'react';
import {
  RenderingEngine,
  Enums,
  getRenderingEngine,
  type Types,
} from '@cornerstonejs/core';
import {
  ToolGroupManager,
  PanTool,
  ZoomTool,
  WindowLevelTool,
  LengthTool,
  StackScrollTool,
  Enums as ToolsEnums,
} from '@cornerstonejs/tools';
import { Contrast, Hand, Loader2, RotateCcw, Ruler, ZoomIn } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import {
  RENDERING_ENGINE_ID,
  TOOL_GROUP_ID,
  VIEWPORT_ID,
  inicializarCornerstone,
} from '@/features/pacs/lib/cornerstone';

const { ViewportType } = Enums;
const { MouseBindings } = ToolsEnums;

type Ferramenta = 'Pan' | 'Zoom' | 'WindowLevel' | 'Length';

const FERRAMENTAS: { id: Ferramenta; nome: string; rotulo: string; icone: typeof Hand }[] = [
  { id: 'WindowLevel', nome: WindowLevelTool.toolName, rotulo: 'Janela/Nível', icone: Contrast },
  { id: 'Pan', nome: PanTool.toolName, rotulo: 'Mover', icone: Hand },
  { id: 'Zoom', nome: ZoomTool.toolName, rotulo: 'Zoom', icone: ZoomIn },
  { id: 'Length', nome: LengthTool.toolName, rotulo: 'Régua', icone: Ruler },
];

type Props = { imageIds: string[]; carregando?: boolean };

export function PacsViewport({ imageIds, carregando }: Props) {
  const elementoRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<RenderingEngine | null>(null);
  const [pronto, setPronto] = useState(false);
  const [ferramentaAtiva, setFerramentaAtiva] = useState<Ferramenta>('WindowLevel');
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let cancelado = false;
    const elemento = elementoRef.current;
    if (!elemento) return;
    let observer: ResizeObserver | null = null;

    void (async () => {
      await inicializarCornerstone();
      if (cancelado) return;

      getRenderingEngine(RENDERING_ENGINE_ID)?.destroy();
      const engine = new RenderingEngine(RENDERING_ENGINE_ID);
      engineRef.current = engine;
      engine.enableElement({
        viewportId: VIEWPORT_ID,
        type: ViewportType.STACK,
        element: elemento,
      });

      let toolGroup = ToolGroupManager.getToolGroup(TOOL_GROUP_ID);
      if (!toolGroup) {
        toolGroup = ToolGroupManager.createToolGroup(TOOL_GROUP_ID)!;
        toolGroup.addTool(PanTool.toolName);
        toolGroup.addTool(ZoomTool.toolName);
        toolGroup.addTool(WindowLevelTool.toolName);
        toolGroup.addTool(LengthTool.toolName);
        toolGroup.addTool(StackScrollTool.toolName);
      }
      toolGroup.addViewport(VIEWPORT_ID, RENDERING_ENGINE_ID);
      toolGroup.setToolActive(StackScrollTool.toolName, {
        bindings: [{ mouseButton: MouseBindings.Wheel }],
      });
      ativarFerramenta('WindowLevel');

      // Mantém o canvas alinhado ao container (e cobre o caso de o layout
      // ainda não estar medido na primeira chamada de enableElement).
      observer = new ResizeObserver(() => engine.resize(true, false));
      observer.observe(elemento);

      setPronto(true);
    })();

    return () => {
      cancelado = true;
      observer?.disconnect();
      ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.removeViewports(
        RENDERING_ENGINE_ID,
        VIEWPORT_ID,
      );
      engineRef.current?.destroy();
      engineRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!pronto || imageIds.length === 0) return;
    const engine = engineRef.current;
    if (!engine) return;
    const vp = engine.getViewport(VIEWPORT_ID) as Types.IStackViewport;
    setErro(null);
    vp.setStack(imageIds, 0)
      .then(() => {
        // Força recálculo do canvas e renderiza — sem isso a primeira imagem
        // pode sair em branco até a próxima interação (ex.: ativar ferramenta).
        engine.resize(true, true);
        vp.render();
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar imagens.'));
  }, [pronto, imageIds]);

  function ativarFerramenta(f: Ferramenta) {
    const tg = ToolGroupManager.getToolGroup(TOOL_GROUP_ID);
    if (!tg) return;
    FERRAMENTAS.forEach((ferr) => tg.setToolPassive(ferr.nome));
    const alvo = FERRAMENTAS.find((ferr) => ferr.id === f)!;
    tg.setToolActive(alvo.nome, { bindings: [{ mouseButton: MouseBindings.Primary }] });
    setFerramentaAtiva(f);
  }

  function resetar() {
    const engine = engineRef.current;
    if (!engine) return;
    const vp = engine.getViewport(VIEWPORT_ID) as Types.IStackViewport;
    vp.resetCamera();
    vp.resetProperties();
    vp.render();
  }

  return (
    <div className="flex flex-1 flex-col bg-black">
      <div className="flex items-center gap-1 border-b border-gray-700 bg-gray-900 px-2 py-1.5">
        {FERRAMENTAS.map((ferr) => (
          <button
            key={ferr.id}
            type="button"
            title={ferr.rotulo}
            onClick={() => ativarFerramenta(ferr.id)}
            className={cn(
              'rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white',
              ferramentaAtiva === ferr.id && 'bg-primary-600 text-white hover:bg-primary-600',
            )}
          >
            <ferr.icone className="h-5 w-5" />
          </button>
        ))}
        <div className="mx-1 h-6 w-px bg-gray-700" />
        <button
          type="button"
          title="Resetar"
          onClick={resetar}
          className="rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white"
        >
          <RotateCcw className="h-5 w-5" />
        </button>
      </div>

      <div className="relative flex-1">
        <div
          ref={elementoRef}
          className="absolute inset-0"
          onContextMenu={(e) => e.preventDefault()}
        />
        {imageIds.length === 0 && !carregando ? (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-sm text-gray-500">
            Selecione uma série para visualizar.
          </div>
        ) : null}
        {carregando ? (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center gap-2 text-sm text-gray-300">
            <Loader2 className="h-5 w-5 animate-spin" /> Carregando imagens...
          </div>
        ) : null}
        {erro ? (
          <div className="absolute inset-x-0 bottom-0 bg-red-900/80 px-4 py-2 text-sm text-red-100">
            {erro}
          </div>
        ) : null}
      </div>
    </div>
  );
}
