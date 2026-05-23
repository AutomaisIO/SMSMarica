import { useCallback, useEffect, useRef, useState } from 'react';
import {
  RenderingEngine,
  Enums,
  eventTarget,
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
  ArrowAnnotateTool,
  annotation as annotationManager,
  Enums as ToolsEnums,
} from '@cornerstonejs/tools';
import {
  Contrast,
  Hand,
  Loader2,
  MessageSquarePlus,
  RotateCcw,
  Ruler,
  Trash2,
  ZoomIn,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import {
  RENDERING_ENGINE_ID,
  TOOL_GROUP_ID,
  VIEWPORT_ID,
  inicializarCornerstone,
  type ProgressoPrefetch,
} from '@/features/pacs/lib/cornerstone';

const { ViewportType } = Enums;
const { MouseBindings } = ToolsEnums;

type Ferramenta = 'Pan' | 'Zoom' | 'WindowLevel' | 'Length' | 'Arrow';

const FERRAMENTAS: { id: Ferramenta; nome: string; rotulo: string; icone: typeof Hand }[] = [
  { id: 'WindowLevel', nome: WindowLevelTool.toolName, rotulo: 'Janela/Nível', icone: Contrast },
  { id: 'Pan', nome: PanTool.toolName, rotulo: 'Mover', icone: Hand },
  { id: 'Zoom', nome: ZoomTool.toolName, rotulo: 'Zoom', icone: ZoomIn },
  { id: 'Length', nome: LengthTool.toolName, rotulo: 'Régua', icone: Ruler },
  { id: 'Arrow', nome: ArrowAnnotateTool.toolName, rotulo: 'Comentário', icone: MessageSquarePlus },
];

/** Pede texto ao usuário ao soltar a seta de comentário. Vazio cancela. */
function aoPedirTexto(callback: (texto: string | null) => void) {
  const txt = window.prompt('Texto do comentário:');
  if (txt == null || txt.trim().length === 0) callback(null);
  else callback(txt.trim());
}

type Props = {
  imageIds: string[];
  carregando?: boolean;
  progresso?: ProgressoPrefetch | null;
};

export function PacsViewport({ imageIds, carregando, progresso }: Props) {
  const elementoRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<RenderingEngine | null>(null);
  const [pronto, setPronto] = useState(false);
  const [ferramentaAtiva, setFerramentaAtiva] = useState<Ferramenta>('WindowLevel');
  const [erro, setErro] = useState<string | null>(null);
  const [qtdSelecionadas, setQtdSelecionadas] = useState(0);
  const [montandoStack, setMontandoStack] = useState(false);

  const excluirSelecao = useCallback(() => {
    const ids = annotationManager.selection.getAnnotationsSelected();
    if (ids.length === 0) return;
    ids.forEach((uid) => annotationManager.state.removeAnnotation(uid));
    annotationManager.selection.deselectAnnotation();
    setQtdSelecionadas(0);
    engineRef.current?.renderViewports([VIEWPORT_ID]);
  }, []);

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
        toolGroup.addTool(ArrowAnnotateTool.toolName, {
          getTextCallback: aoPedirTexto,
        });
      }
      toolGroup.addViewport(VIEWPORT_ID, RENDERING_ENGINE_ID);
      ativarFerramenta('WindowLevel');

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

  // Listener de seleção de annotations.
  useEffect(() => {
    function aoMudarSelecao() {
      setQtdSelecionadas(annotationManager.selection.getAnnotationsSelected().length);
    }
    eventTarget.addEventListener(ToolsEnums.Events.ANNOTATION_SELECTION_CHANGE, aoMudarSelecao);
    return () => {
      eventTarget.removeEventListener(ToolsEnums.Events.ANNOTATION_SELECTION_CHANGE, aoMudarSelecao);
    };
  }, []);

  // Tecla Delete/Backspace remove a annotation selecionada (se não estiver digitando).
  useEffect(() => {
    function aoTeclar(e: KeyboardEvent) {
      if (e.key !== 'Delete' && e.key !== 'Backspace') return;
      const ativo = document.activeElement;
      const tag = ativo?.tagName ?? '';
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if ((ativo as HTMLElement | null)?.isContentEditable) return;
      if (annotationManager.selection.getAnnotationsSelectedCount() === 0) return;
      e.preventDefault();
      excluirSelecao();
    }
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, [excluirSelecao]);

  // Monta a stack no viewport e mantém o overlay visível até `setStack` resolver
  // (não só até a Promise dos metadados resolver). Cancela ao trocar imageIds.
  // Nada de `engine.resize` aqui — o ResizeObserver no init já mantém o canvas
  // alinhado e chamar resize a cada troca de série custa caro à toa.
  useEffect(() => {
    if (!pronto) return;
    const engine = engineRef.current;
    if (!engine) return;

    if (imageIds.length === 0) {
      setMontandoStack(false);
      return;
    }

    const vp = engine.getViewport(VIEWPORT_ID) as Types.IStackViewport;
    setErro(null);
    setMontandoStack(true);

    let cancelado = false;
    vp.setStack(imageIds, 0)
      .then(() => {
        if (cancelado) return;
        vp.render();
      })
      .catch((e) => {
        if (cancelado) return;
        setErro(e instanceof Error ? e.message : 'Falha ao carregar imagens.');
      })
      .finally(() => {
        if (cancelado) return;
        setMontandoStack(false);
      });

    return () => {
      cancelado = true;
    };
  }, [pronto, imageIds]);

  // Debounce do overlay de "Carregando imagem..." para o `montandoStack`:
  // se a stack vier do cache (resolução abaixo de ~150ms), o overlay nem
  // chega a aparecer — sem piscar a cada clique numa série já bufferizada.
  const [mostrarOverlayStack, setMostrarOverlayStack] = useState(false);
  useEffect(() => {
    if (!montandoStack) {
      setMostrarOverlayStack(false);
      return;
    }
    const t = window.setTimeout(() => setMostrarOverlayStack(true), 150);
    return () => window.clearTimeout(t);
  }, [montandoStack]);

  function ativarFerramenta(f: Ferramenta) {
    const tg = ToolGroupManager.getToolGroup(TOOL_GROUP_ID);
    if (!tg) return;

    // Desativa todas as primárias; o Zoom é tratado à parte porque mantém o Wheel.
    FERRAMENTAS.forEach((ferr) => {
      if (ferr.id !== 'Zoom') tg.setToolPassive(ferr.nome);
    });

    if (f === 'Zoom') {
      // Zoom ganha Primary + Wheel.
      tg.setToolActive(ZoomTool.toolName, {
        bindings: [
          { mouseButton: MouseBindings.Primary },
          { mouseButton: MouseBindings.Wheel },
        ],
      });
    } else {
      // Zoom sempre mantém Wheel; o scroll do mouse aplica zoom in/out em qualquer modo.
      tg.setToolActive(ZoomTool.toolName, {
        bindings: [{ mouseButton: MouseBindings.Wheel }],
      });
      const alvo = FERRAMENTAS.find((ferr) => ferr.id === f)!;
      tg.setToolActive(alvo.nome, { bindings: [{ mouseButton: MouseBindings.Primary }] });
    }
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
          title={
            qtdSelecionadas > 0
              ? `Excluir ${qtdSelecionadas} marca${qtdSelecionadas > 1 ? 's' : ''} (Del)`
              : 'Selecione uma marca para excluir'
          }
          onClick={excluirSelecao}
          disabled={qtdSelecionadas === 0}
          className={cn(
            'rounded-md p-2 transition-colors',
            qtdSelecionadas > 0
              ? 'text-red-300 hover:bg-red-900/40 hover:text-red-100'
              : 'cursor-not-allowed text-gray-600',
          )}
        >
          <Trash2 className="h-5 w-5" />
        </button>
        <button
          type="button"
          title="Resetar"
          onClick={resetar}
          className="rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white"
        >
          <RotateCcw className="h-5 w-5" />
        </button>

        <div className="ml-auto pr-1 text-[11px] text-gray-500">
          Scroll = zoom · clique numa marca + Del para excluir
        </div>
      </div>

      {/* Barra fina de prefetch — só aparece enquanto o estudo está sendo
          baixado em background. Sai discretamente quando completa. */}
      {progresso && progresso.total > 0 && progresso.carregadas < progresso.total ? (
        <div className="h-0.5 w-full bg-gray-800">
          <div
            className="h-full bg-primary-500 transition-[width] duration-200 ease-out"
            style={{ width: `${(progresso.carregadas / progresso.total) * 100}%` }}
          />
        </div>
      ) : null}

      <div className="relative flex-1">
        <div
          ref={elementoRef}
          className="absolute inset-0"
          onContextMenu={(e) => e.preventDefault()}
        />
        {/* Overlay opaco unificado — cobre o canvas até a stack atual estar
            renderizada, evitando flash da imagem anterior entre uma seleção
            e outra. Para `montandoStack` usamos o estado debounced, então
            cache hit (rápido) não pisca; só aparece se realmente demorar. */}
        {imageIds.length === 0 || carregando || mostrarOverlayStack ? (
          <div className="absolute inset-0 flex items-center justify-center gap-2 bg-black text-sm text-gray-300">
            {carregando || mostrarOverlayStack ? (
              <>
                <Loader2 className="h-5 w-5 animate-spin" />
                Carregando imagem...
              </>
            ) : (
              <span className="text-gray-500">Selecione uma série para visualizar.</span>
            )}
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
