import { useCallback, useEffect, useRef, useState } from 'react';
import {
  RenderingEngine,
  EVENTS,
  eventTarget,
  getRenderingEngine,
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
  FlipHorizontal2,
  FlipVertical2,
  Hand,
  History,
  Loader2,
  Maximize2,
  MessageSquarePlus,
  RotateCcw,
  RotateCw,
  Ruler,
  Save,
  ScanSearch,
  Spline,
  SunMoon,
  Trash2,
  ZoomIn,
} from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import {
  RENDERING_ENGINE_ID,
  TOOL_GROUP_ID,
  idViewportCelula,
  inicializarCornerstone,
  type ProgressoPrefetch,
} from '@/features/pacs/lib/cornerstone';
import {
  removerTodasAnotacoes,
  restaurarAnotacoes,
  serializarAnotacoes,
} from '@/features/pacs/lib/anotacoes';
import { obterVersaoAtual, salvarVersao } from '@/features/pacs/api/anotacoesApi';
import { PacsHistoricoAnotacoesModal } from '@/features/pacs/components/PacsHistoricoAnotacoesModal';
import { PacsViewportCelula } from '@/features/pacs/components/PacsViewportCelula';
import { SeletorLayoutGrade, type Layout } from '@/features/pacs/components/SeletorLayoutGrade';
import type { EstudoAnotacaoVersao } from '@/features/pacs/types';

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
  /** imageId por quadrado (comprimento = linhas*colunas); null = vazio. */
  imagensPorCelula: (string | null)[];
  layout: Layout;
  /** Índice do quadrado em foco — alvo dos botões girar/flip/negativo/reset. */
  focado: number;
  aoFocar: (i: number) => void;
  aoMudarLayout: (l: Layout) => void;
  carregando?: boolean;
  progresso?: ProgressoPrefetch | null;
  /**
   * Quando presente, habilita persistência: carrega a última versão de
   * anotações ao montar e expõe os botões "Salvar" e "Histórico".
   */
  studyInstanceUID?: string | null;
};

export function PacsViewport({
  imagensPorCelula,
  layout,
  focado,
  aoFocar,
  aoMudarLayout,
  carregando,
  progresso,
  studyInstanceUID,
}: Props) {
  const podeSalvarAnotacoes = usePermissao('Pacs', 'Edicao');
  const gridRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<RenderingEngine | null>(null);
  const ouvinteHabilitarRef = useRef<((e: Event) => void) | null>(null);
  const [engine, setEngine] = useState<RenderingEngine | null>(null);
  const [pronto, setPronto] = useState(false);
  const [ferramentaAtiva, setFerramentaAtiva] = useState<Ferramenta>('WindowLevel');
  const [qtdSelecionadas, setQtdSelecionadas] = useState(0);
  const [negativoAtivo, setNegativoAtivo] = useState(false);
  const [versaoAtual, setVersaoAtual] = useState<EstudoAnotacaoVersao | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erroAnotacao, setErroAnotacao] = useState<string | null>(null);
  const [historicoAberto, setHistoricoAberto] = useState(false);

  /** Renderiza todas as viewports ativas da engine. */
  const renderTodas = useCallback(() => {
    const eng = engineRef.current;
    if (!eng) return;
    const ids = eng.getViewports().map((v) => v.id);
    if (ids.length) eng.renderViewports(ids);
  }, []);

  /** StackViewport do quadrado em foco (ou null). */
  const viewportFocado = useCallback((): Types.IStackViewport | null => {
    const eng = engineRef.current;
    if (!eng) return null;
    try {
      return (eng.getViewport(idViewportCelula(focado)) as Types.IStackViewport) ?? null;
    } catch {
      return null;
    }
  }, [focado]);

  const excluirSelecao = useCallback(() => {
    const ids = annotationManager.selection.getAnnotationsSelected();
    if (ids.length === 0) return;
    ids.forEach((uid) => annotationManager.state.removeAnnotation(uid));
    annotationManager.selection.deselectAnnotation();
    setQtdSelecionadas(0);
    renderTodas();
  }, [renderTodas]);

  // A seleção do annotationManager é global. Quando uma célula troca de imagem,
  // limpamos a seleção para o botão de lixeira (ou Delete) não apagar uma marca
  // que não está mais visível. O STACK_NEW_IMAGE só dispara no elemento da
  // viewport, então cada célula chama isto via callback.
  const deselecionarTudo = useCallback(() => {
    if (annotationManager.selection.getAnnotationsSelectedCount() === 0) return;
    annotationManager.selection.deselectAnnotation();
    setQtdSelecionadas(0);
  }, []);

  // Init: cria a RenderingEngine e o ToolGroup uma vez. A engine vive enquanto
  // o container existir; as células anexam/desanexam suas viewports nela.
  useEffect(() => {
    let cancelado = false;

    void (async () => {
      await inicializarCornerstone();
      if (cancelado) return;

      getRenderingEngine(RENDERING_ENGINE_ID)?.destroy();
      const eng = new RenderingEngine(RENDERING_ENGINE_ID);
      engineRef.current = eng;

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
        toolGroup.addTool(ArrowAnnotateTool.toolName, { getTextCallback: aoPedirTexto });
        toolGroup.addTool(ScaleOverlayXYTool.toolName);
      }
      // Escala em mm nos eixos (estilo Weasis) — sempre visível, sem interação.
      toolGroup.setToolEnabled(ScaleOverlayXYTool.toolName);

      // Quando o MagnifyTool cria seu próprio viewport ('magnify-viewport'),
      // anexamos ao nosso ToolGroup — assim a régua mm também aparece na lupa.
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

      setEngine(eng);
      setPronto(true);
      ativarFerramenta('WindowLevel');
    })();

    return () => {
      cancelado = true;
      if (ouvinteHabilitarRef.current) {
        eventTarget.removeEventListener(EVENTS.ELEMENT_ENABLED, ouvinteHabilitarRef.current);
        ouvinteHabilitarRef.current = null;
      }
      engineRef.current?.destroy();
      engineRef.current = null;
      setEngine(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Reaplica o resize da engine quando o layout muda (células entram/saem) e
  // quando o container é redimensionado (painel/janela). Os efeitos das células
  // já registraram/desregistraram suas viewports antes deste efeito do pai.
  useEffect(() => {
    if (!pronto) return;
    engineRef.current?.resize(true, false);
  }, [pronto, layout.linhas, layout.colunas]);

  useEffect(() => {
    if (!pronto) return;
    const grid = gridRef.current;
    if (!grid) return;
    const observer = new ResizeObserver(() => engineRef.current?.resize(true, false));
    observer.observe(grid);
    return () => observer.disconnect();
  }, [pronto]);

  // Listener de seleção de annotations (global ao Cornerstone).
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

  // Carrega a versão mais recente das anotações ao abrir um estudo. Limpa o
  // estado quando o studyUID muda (ou some).
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
        renderTodas();
      })
      .catch((e) => {
        if (!cancelado) setErroAnotacao(extrairMensagemDeErro(e));
      });
    return () => {
      cancelado = true;
    };
  }, [pronto, studyInstanceUID, renderTodas]);

  // Reflete o estado de negativo (invert) do quadrado em foco no realce do botão.
  useEffect(() => {
    if (!pronto) return;
    const vp = viewportFocado();
    setNegativoAtivo(Boolean(vp?.getProperties().invert));
  }, [pronto, focado, imagensPorCelula, viewportFocado]);

  function ativarFerramenta(f: Ferramenta) {
    const tg = ToolGroupManager.getToolGroup(TOOL_GROUP_ID);
    if (!tg) return;

    // Desativa todas as primárias; o Zoom é tratado à parte porque mantém o Wheel.
    FERRAMENTAS.forEach((ferr) => {
      if (ferr.id !== 'Zoom') tg.setToolPassive(ferr.nome);
    });

    if (f === 'Zoom') {
      tg.setToolActive(ZoomTool.toolName, {
        bindings: [
          { mouseButton: MouseBindings.Primary },
          { mouseButton: MouseBindings.Wheel },
        ],
      });
    } else {
      // Zoom sempre mantém Wheel; o scroll do mouse aplica zoom em qualquer modo.
      tg.setToolActive(ZoomTool.toolName, { bindings: [{ mouseButton: MouseBindings.Wheel }] });
      const alvo = FERRAMENTAS.find((ferr) => ferr.id === f)!;
      tg.setToolActive(alvo.nome, { bindings: [{ mouseButton: MouseBindings.Primary }] });
    }
    setFerramentaAtiva(f);
  }

  /** Transformações agem no quadrado em foco e só se ele tiver imagem. */
  function comViewportFocado(fn: (vp: Types.IStackViewport) => void) {
    const vp = viewportFocado();
    if (!vp || !vp.getCurrentImageId?.()) return;
    fn(vp);
    vp.render();
  }

  function girar(graus: 90 | -90) {
    comViewportFocado((vp) => {
      const r = (((vp.getRotation() + graus) % 360) + 360) % 360;
      vp.setViewPresentation({ rotation: r });
    });
  }

  function espelhar(eixo: 'h' | 'v') {
    comViewportFocado((vp) => {
      // Via setCamera (não setViewPresentation): este último tem um toggle
      // relativo que liga o flip mas não desliga (passa `false` ao flip()
      // interno, que só inverte com valor truthy). setCamera calcula a
      // diferença e alterna corretamente nos dois sentidos.
      const cam = vp.getCamera();
      vp.setCamera(
        eixo === 'h'
          ? { flipHorizontal: !cam.flipHorizontal }
          : { flipVertical: !cam.flipVertical },
      );
    });
  }

  function negativo() {
    comViewportFocado((vp) => {
      const novo = !vp.getProperties().invert;
      vp.setProperties({ invert: novo });
      setNegativoAtivo(novo);
    });
  }

  function resetar() {
    comViewportFocado((vp) => {
      vp.resetCamera();
      vp.resetProperties();
      setNegativoAtivo(false);
    });
  }

  async function salvarAnotacoes() {
    if (!studyInstanceUID) return;
    const comentario = window.prompt(
      'Comentário sobre esta versão (opcional):',
      versaoAtual?.comentario ?? '',
    );
    if (comentario === null) return; // Cancel

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
    renderTodas();
  }

  const botaoBase =
    'rounded-md p-2 text-gray-300 transition-colors hover:bg-gray-700 hover:text-white';

  return (
    <div className="flex flex-1 flex-col bg-black">
      <div className="flex flex-wrap items-center gap-1 border-b border-gray-700 bg-gray-900 px-2 py-1.5">
        {FERRAMENTAS.map((ferr) => (
          <button
            key={ferr.id}
            type="button"
            title={ferr.rotulo}
            onClick={() => ativarFerramenta(ferr.id)}
            className={cn(
              botaoBase,
              ferramentaAtiva === ferr.id && 'bg-primary-600 text-white hover:bg-primary-600',
            )}
          >
            <ferr.icone className="h-5 w-5" />
          </button>
        ))}

        <div className="mx-1 h-6 w-px bg-gray-700" />

        {/* Transformações — agem no quadrado em foco. */}
        <button type="button" title="Girar anti-horário" onClick={() => girar(-90)} className={botaoBase}>
          <RotateCcw className="h-5 w-5" />
        </button>
        <button type="button" title="Girar horário" onClick={() => girar(90)} className={botaoBase}>
          <RotateCw className="h-5 w-5" />
        </button>
        <button type="button" title="Espelhar horizontal" onClick={() => espelhar('h')} className={botaoBase}>
          <FlipHorizontal2 className="h-5 w-5" />
        </button>
        <button type="button" title="Espelhar vertical" onClick={() => espelhar('v')} className={botaoBase}>
          <FlipVertical2 className="h-5 w-5" />
        </button>
        <button
          type="button"
          title="Negativo (inverter)"
          onClick={negativo}
          className={cn(botaoBase, negativoAtivo && 'bg-primary-600 text-white hover:bg-primary-600')}
        >
          <SunMoon className="h-5 w-5" />
        </button>

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
        <button type="button" title="Resetar (foco)" onClick={resetar} className={botaoBase}>
          <Maximize2 className="h-5 w-5" />
        </button>

        <div className="mx-1 h-6 w-px bg-gray-700" />

        <SeletorLayoutGrade valor={layout} onSelecionar={aoMudarLayout} />

        {studyInstanceUID ? (
          <>
            <div className="mx-1 h-6 w-px bg-gray-700" />
            {podeSalvarAnotacoes ? (
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
            ) : null}
            <button
              type="button"
              title="Histórico de anotações"
              onClick={() => setHistoricoAberto(true)}
              className={botaoBase}
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

      {/* Barra fina de prefetch — só enquanto o estudo baixa em background. */}
      {progresso && progresso.total > 0 && progresso.carregadas < progresso.total ? (
        <div className="h-0.5 w-full bg-gray-800">
          <div
            className="h-full bg-primary-500 transition-[width] duration-200 ease-out"
            style={{ width: `${(progresso.carregadas / progresso.total) * 100}%` }}
          />
        </div>
      ) : null}

      {erroAnotacao ? (
        <div className="flex items-start justify-between gap-3 bg-amber-900/80 px-4 py-2 text-sm text-amber-50">
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

      <div
        ref={gridRef}
        className="grid flex-1 gap-0.5 bg-gray-700"
        style={{
          gridTemplateColumns: `repeat(${layout.colunas}, minmax(0, 1fr))`,
          gridTemplateRows: `repeat(${layout.linhas}, minmax(0, 1fr))`,
        }}
      >
        {imagensPorCelula.map((imageId, i) => (
          <PacsViewportCelula
            key={i}
            engine={engine}
            viewportId={idViewportCelula(i)}
            imageId={imageId}
            focado={i === focado}
            aoFocar={() => aoFocar(i)}
            aoNovaImagem={deselecionarTudo}
            carregando={carregando}
          />
        ))}
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
