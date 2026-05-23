import { useCallback, useEffect, useRef, useState } from 'react';
import {
  RenderingEngine,
  Enums,
  EVENTS,
  eventTarget,
  getRenderingEngine,
  metaData,
  type Types,
} from '@cornerstonejs/core';
import {
  ToolGroupManager,
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
  annotation as annotationManager,
  Enums as ToolsEnums,
} from '@cornerstonejs/tools';
import { ScaleOverlayXYTool } from '@/features/pacs/lib/scaleOverlayXY';
import {
  CircleDashed,
  Contrast,
  Crosshair,
  Hand,
  History,
  Loader2,
  MessageSquarePlus,
  RotateCcw,
  Ruler,
  Save,
  ScanSearch,
  Spline,
  Trash2,
  ZoomIn,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import {
  RENDERING_ENGINE_ID,
  TOOL_GROUP_ID,
  VIEWPORT_ID,
  inicializarCornerstone,
  type ProgressoPrefetch,
} from '@/features/pacs/lib/cornerstone';
import { fontePixelSpacing } from '@/features/pacs/lib/dicomJson';
import {
  removerTodasAnotacoes,
  restaurarAnotacoes,
  serializarAnotacoes,
} from '@/features/pacs/lib/anotacoes';
import { obterVersaoAtual, salvarVersao } from '@/features/pacs/api/anotacoesApi';
import { PacsHistoricoAnotacoesModal } from '@/features/pacs/components/PacsHistoricoAnotacoesModal';
import type { EstudoAnotacaoVersao } from '@/features/pacs/types';

const { ViewportType } = Enums;
const { MouseBindings } = ToolsEnums;

type Ferramenta =
  | 'Pan'
  | 'Zoom'
  | 'WindowLevel'
  | 'Length'
  | 'Angle'
  | 'EllipticalROI'
  | 'Probe'
  | 'Magnify'
  | 'Arrow';

const FERRAMENTAS: { id: Ferramenta; nome: string; rotulo: string; icone: typeof Hand }[] = [
  { id: 'WindowLevel', nome: WindowLevelTool.toolName, rotulo: 'Janela/Nível', icone: Contrast },
  { id: 'Pan', nome: PanTool.toolName, rotulo: 'Mover', icone: Hand },
  { id: 'Zoom', nome: ZoomTool.toolName, rotulo: 'Zoom', icone: ZoomIn },
  { id: 'Magnify', nome: MagnifyTool.toolName, rotulo: 'Lupa', icone: ScanSearch },
  { id: 'Length', nome: LengthTool.toolName, rotulo: 'Régua', icone: Ruler },
  { id: 'Angle', nome: AngleTool.toolName, rotulo: 'Ângulo', icone: Spline },
  { id: 'EllipticalROI', nome: EllipticalROITool.toolName, rotulo: 'ROI elíptica', icone: CircleDashed },
  { id: 'Probe', nome: ProbeTool.toolName, rotulo: 'Intensidade do pixel', icone: Crosshair },
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
  /**
   * Quando presente, habilita persistência: o viewport carrega a última versão
   * de anotações ao montar e expõe os botões "Salvar" e "Histórico".
   */
  studyInstanceUID?: string | null;
};

export function PacsViewport({ imageIds, carregando, progresso, studyInstanceUID }: Props) {
  const elementoRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<RenderingEngine | null>(null);
  const ouvinteHabilitarRef = useRef<((e: Event) => void) | null>(null);
  const [pronto, setPronto] = useState(false);
  const [ferramentaAtiva, setFerramentaAtiva] = useState<Ferramenta>('WindowLevel');
  const [erro, setErro] = useState<string | null>(null);
  const [qtdSelecionadas, setQtdSelecionadas] = useState(0);
  const [montandoStack, setMontandoStack] = useState(false);
  const [infoFooter, setInfoFooter] = useState<{
    zoom: number;
    rowPixelSpacing: number | null;
    columnPixelSpacing: number | null;
    fonte: 'equipamento' | 'estimado' | 'ausente';
  } | null>(null);
  const [versaoAtual, setVersaoAtual] = useState<EstudoAnotacaoVersao | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erroAnotacao, setErroAnotacao] = useState<string | null>(null);
  const [historicoAberto, setHistoricoAberto] = useState(false);

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
        toolGroup.addTool(AngleTool.toolName);
        toolGroup.addTool(EllipticalROITool.toolName);
        toolGroup.addTool(ProbeTool.toolName);
        toolGroup.addTool(MagnifyTool.toolName);
        toolGroup.addTool(ArrowAnnotateTool.toolName, {
          getTextCallback: aoPedirTexto,
        });
        toolGroup.addTool(ScaleOverlayXYTool.toolName);
      }
      toolGroup.addViewport(VIEWPORT_ID, RENDERING_ENGINE_ID);
      // Escala em mm nos eixos (estilo Weasis) — sempre visível, sem interação.
      toolGroup.setToolEnabled(ScaleOverlayXYTool.toolName);
      ativarFerramenta('WindowLevel');

      // Quando o MagnifyTool cria seu próprio viewport ('magnify-viewport'),
      // anexamos ao nosso ToolGroup — isso faz o ScaleOverlayTool desenhar a
      // régua mm também dentro da janela da lupa.
      const aoHabilitar = (e: Event) => {
        const detalhe = (e as CustomEvent).detail as { viewportId?: string } | undefined;
        if (detalhe?.viewportId === 'magnify-viewport') {
          ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.addViewport(
            'magnify-viewport',
            RENDERING_ENGINE_ID,
          );
        }
      };
      eventTarget.addEventListener(EVENTS.ELEMENT_ENABLED, aoHabilitar);
      ouvinteHabilitarRef.current = aoHabilitar;

      observer = new ResizeObserver(() => engine.resize(true, false));
      observer.observe(elemento);

      setPronto(true);
    })();

    return () => {
      cancelado = true;
      observer?.disconnect();
      if (ouvinteHabilitarRef.current) {
        eventTarget.removeEventListener(EVENTS.ELEMENT_ENABLED, ouvinteHabilitarRef.current);
        ouvinteHabilitarRef.current = null;
      }
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

  // A seleção do annotationManager é global (não amarrada ao imageId/viewport
  // exibido). Sem este reset, ao trocar de imagem a seleção da anterior fica
  // viva — e o botão de lixeira (ou Delete no teclado) acaba apagando uma
  // marca que não está mais visível. Limpamos a cada STACK_NEW_IMAGE, que
  // cobre tanto scroll dentro da stack quanto troca de série/estudo.
  useEffect(() => {
    if (!pronto) return;
    const elemento = elementoRef.current;
    if (!elemento) return;

    function deselecionar() {
      if (annotationManager.selection.getAnnotationsSelectedCount() === 0) return;
      annotationManager.selection.deselectAnnotation();
      setQtdSelecionadas(0);
    }
    elemento.addEventListener(EVENTS.STACK_NEW_IMAGE, deselecionar);
    return () => {
      elemento.removeEventListener(EVENTS.STACK_NEW_IMAGE, deselecionar);
    };
  }, [pronto]);

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
        // resetCamera enquadra a imagem (fit-to-viewport) usando Rows/Columns
        // do metadata. Sem ele, ao trocar de série a câmera mantém o zoom da
        // imagem anterior e pode ficar cortada/com sobra.
        vp.resetCamera();
        vp.render();
        // Re-habilita ScaleOverlay agora que a imagem está montada: o tool
        // captura os cantos da imagem em `setToolEnabled` via
        // `getViewportImageCornersInWorld`, que retorna [] quando o viewport
        // ainda não tem imageData. Sem este re-enable, a régua nasce com
        // points vazios no init inicial e nunca aparece sobre a imagem.
        ToolGroupManager.getToolGroup(TOOL_GROUP_ID)?.setToolEnabled(
          ScaleOverlayXYTool.toolName,
        );
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

  // Atualiza o footer (zoom + px/mm + fonte) sempre que a câmera mexer ou a
  // imagem atual trocar. Lê do StackViewport corrente.
  useEffect(() => {
    if (!pronto) return;
    const elemento = elementoRef.current;
    if (!elemento) return;

    function atualizar() {
      const engine = engineRef.current;
      if (!engine) return;
      const vp = engine.getViewport(VIEWPORT_ID) as Types.IStackViewport | undefined;
      if (!vp) return;
      const imageId = vp.getCurrentImageId?.();
      if (!imageId) {
        setInfoFooter(null);
        return;
      }
      const plano = metaData.get('imagePlaneModule', imageId) as
        | { rowPixelSpacing?: number; columnPixelSpacing?: number }
        | undefined;
      setInfoFooter({
        zoom: vp.getZoom(),
        rowPixelSpacing: plano?.rowPixelSpacing ?? null,
        columnPixelSpacing: plano?.columnPixelSpacing ?? null,
        fonte: fontePixelSpacing(imageId),
      });
    }

    // Eventos disparados no próprio elemento do viewport.
    elemento.addEventListener(EVENTS.CAMERA_MODIFIED, atualizar);
    elemento.addEventListener(EVENTS.STACK_NEW_IMAGE, atualizar);
    elemento.addEventListener(EVENTS.IMAGE_RENDERED, atualizar);
    return () => {
      elemento.removeEventListener(EVENTS.CAMERA_MODIFIED, atualizar);
      elemento.removeEventListener(EVENTS.STACK_NEW_IMAGE, atualizar);
      elemento.removeEventListener(EVENTS.IMAGE_RENDERED, atualizar);
    };
  }, [pronto]);

  // Quando a stack muda (nova série), reseta o footer pra não mostrar info da
  // imagem anterior enquanto a nova ainda não renderizou.
  useEffect(() => {
    if (imageIds.length === 0) setInfoFooter(null);
  }, [imageIds]);

  // Carrega a versão mais recente das anotações ao abrir um estudo. Limpa o
  // estado quando o studyUID muda (ou some), garantindo que anotações do estudo
  // anterior nunca "vazem" para o atual.
  useEffect(() => {
    if (!pronto) return;
    removerTodasAnotacoes();
    setVersaoAtual(null);
    setErroAnotacao(null);
    if (!studyInstanceUID) return;

    let cancelado = false;
    obterVersaoAtual(studyInstanceUID)
      .then((dto) => {
        if (cancelado || !dto) return;
        restaurarAnotacoes(dto.payload);
        setVersaoAtual(dto);
        engineRef.current?.renderViewports([VIEWPORT_ID]);
      })
      .catch((e) => {
        if (!cancelado) setErroAnotacao(extrairMensagemDeErro(e));
      });
    return () => {
      cancelado = true;
    };
  }, [pronto, studyInstanceUID]);

  async function salvarAnotacoes() {
    if (!studyInstanceUID) return;
    const comentario = window.prompt(
      'Comentário sobre esta versão (opcional):',
      versaoAtual?.comentario ?? '',
    );
    // null = clicou em Cancel → aborta. String vazia = ok sem comentário.
    if (comentario === null) return;

    setSalvando(true);
    setErroAnotacao(null);
    try {
      const payload = serializarAnotacoes();
      const dto = await salvarVersao(studyInstanceUID, payload, comentario.trim() || null);
      setVersaoAtual(dto);
    } catch (e) {
      setErroAnotacao(extrairMensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  function restaurarVersaoHistorica(dto: EstudoAnotacaoVersao) {
    restaurarAnotacoes(dto.payload);
    setVersaoAtual(dto);
    engineRef.current?.renderViewports([VIEWPORT_ID]);
  }

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

        {studyInstanceUID ? (
          <>
            <div className="mx-1 h-6 w-px bg-gray-700" />
            <button
              type="button"
              title="Salvar anotações"
              onClick={salvarAnotacoes}
              disabled={salvando}
              className={cn(
                'rounded-md p-2 transition-colors',
                salvando
                  ? 'cursor-wait text-gray-500'
                  : 'text-emerald-300 hover:bg-emerald-900/30 hover:text-emerald-100',
              )}
            >
              {salvando ? (
                <Loader2 className="h-5 w-5 animate-spin" />
              ) : (
                <Save className="h-5 w-5" />
              )}
            </button>
            <button
              type="button"
              title="Histórico de anotações"
              onClick={() => setHistoricoAberto(true)}
              className="rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white"
            >
              <History className="h-5 w-5" />
            </button>
          </>
        ) : null}

        <div className="ml-auto flex items-center gap-3 pr-1 text-[11px] text-gray-500">
          {versaoAtual ? (
            <span title={`Última versão salva — ${formatarDataHoraCurta(versaoAtual.criadoEm)}`}>
              v{versaoAtual.versao} · {versaoAtual.usuarioNome.split(' ')[0]}
            </span>
          ) : null}
          <span>Scroll = zoom · clique numa marca + Del para excluir</span>
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
        {erroAnotacao ? (
          <div className="absolute inset-x-0 top-0 flex items-start justify-between gap-3 bg-amber-900/80 px-4 py-2 text-sm text-amber-50">
            <span>Anotações: {erroAnotacao}</span>
            <button
              type="button"
              className="rounded p-0.5 hover:bg-amber-800/60"
              onClick={() => setErroAnotacao(null)}
              aria-label="Fechar aviso"
            >
              ×
            </button>
          </div>
        ) : null}

        {/* Footer-info: zoom + resolução + fonte do PixelSpacing. */}
        {infoFooter && imageIds.length > 0 && !montandoStack ? (
          <div className="pointer-events-none absolute bottom-2 left-3 select-none font-mono text-[11px] leading-tight text-gray-400 mix-blend-screen">
            <div>Zoom: {(infoFooter.zoom * 100).toFixed(0)}%</div>
            {infoFooter.rowPixelSpacing != null ? (
              <div>
                {formatarSpacing(infoFooter.rowPixelSpacing, infoFooter.columnPixelSpacing)}
              </div>
            ) : null}
            <div className={infoFooter.fonte === 'estimado' ? 'text-amber-400' : ''}>
              {labelFonte(infoFooter.fonte)}
            </div>
          </div>
        ) : null}
      </div>

      {studyInstanceUID ? (
        <PacsHistoricoAnotacoesModal
          aberto={historicoAberto}
          aoFechar={() => setHistoricoAberto(false)}
          studyInstanceUID={studyInstanceUID}
          versaoAtual={versaoAtual?.versao ?? null}
          aoRestaurar={restaurarVersaoHistorica}
        />
      ) : null}
    </div>
  );
}

function formatarDataHoraCurta(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatarSpacing(row: number, col: number | null): string {
  const r = row.toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
  if (col == null || Math.abs(row - col) < 1e-6) return `${r} mm/px`;
  const c = col.toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
  return `${r} × ${c} mm/px`;
}

function labelFonte(fonte: 'equipamento' | 'estimado' | 'ausente'): string {
  if (fonte === 'equipamento') return 'Calibração: equipamento';
  if (fonte === 'estimado') return 'Calibração: estimada (detector)';
  return 'Calibração: ausente';
}
