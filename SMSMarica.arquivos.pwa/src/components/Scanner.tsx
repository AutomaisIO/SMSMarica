import { useEffect, useRef, useState } from 'react';
import {
  ArrowDown,
  ArrowUp,
  Camera,
  Check,
  Crop,
  ImagePlus,
  RotateCcw,
  ScanLine,
  Trash2,
  X,
} from 'lucide-react';
import {
  arquivoParaDataUrl,
  prepararScanner,
  realcarDataUrl,
  type OpcaoRealce,
} from '@/lib/scanner';
import { EditorRecorte } from '@/components/EditorRecorte';
import { GhostButton, PrimaryButton, Spinner } from '@/components/ui';
import { cn } from '@/lib/cn';

type PaginaCapturada = {
  id: string;
  dataUrl: string;
  /** `true` quando houve recorte manual (vs "página inteira"). */
  recortado: boolean;
};

type Revisao = {
  /** dataURL da imagem JÁ recortada (entrada do realce); reaplicamos só o realce ao trocar. */
  recortada: string;
  /** Veio de recorte manual (`true`) ou de "página inteira" (`false`). */
  recortado: boolean;
  /** dataURL final já com o realce aplicado. */
  resultado: string | null;
  realce: OpcaoRealce;
  processando: boolean;
};

type Vista = 'lista' | 'camera' | 'editor' | 'revisao';

const novoId = () =>
  (crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`);

/**
 * Digitalizador de páginas. Fluxo por página: captura (câmera ao vivo ou
 * <input type=file capture=environment>, caminho confiável no iOS) → editor de
 * recorte manual (4 cantos arrastáveis estilo CamScanner) → revisão de realce
 * (Documento P/B x Cor, só contraste sobre a imagem já recortada) → aceitar.
 * Devolve a lista de imagens prontas para virar PDF.
 */
export function Scanner({
  aoConcluir,
  aoCancelar,
}: {
  aoConcluir: (paginas: string[]) => void;
  aoCancelar: () => void;
}) {
  const [paginas, setPaginas] = useState<PaginaCapturada[]>([]);
  const [vista, setVista] = useState<Vista>('lista');
  const [fotoCrua, setFotoCrua] = useState<string | null>(null); // foto crua em edição (recorte)
  const [revisao, setRevisao] = useState<Revisao | null>(null);
  const [scannerPronto, setScannerPronto] = useState<boolean | null>(null);
  const [confirmar, setConfirmar] = useState<'fechar' | 'concluir' | null>(null);
  const inputArquivo = useRef<HTMLInputElement>(null);

  // getUserMedia indica suporte a preview ao vivo. No iOS Safari isso pode existir
  // mas ser instável dentro de PWA standalone — por isso o <input type=file> abaixo
  // é sempre oferecido como caminho garantido (abre a câmera nativa do aparelho).
  const temCameraAoVivo =
    typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia;

  // Pré-aquece o download do OpenCV assim que o scanner abre (não bloqueia a UI).
  useEffect(() => {
    let vivo = true;
    void prepararScanner().then((ok) => {
      if (vivo) setScannerPronto(ok);
    });
    return () => {
      vivo = false;
    };
  }, []);

  function abrirCaptura() {
    if (temCameraAoVivo) setVista('camera');
    else inputArquivo.current?.click();
  }

  // Foto crua capturada (câmera/arquivo) → editor de recorte manual.
  function aoCapturarFoto(dataUrl: string) {
    setFotoCrua(dataUrl);
    setVista('editor');
  }

  async function aoEscolherArquivo(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    e.target.value = ''; // permite re-escolher o mesmo arquivo depois
    if (!arquivo) return;
    const dataUrl = await arquivoParaDataUrl(arquivo);
    aoCapturarFoto(dataUrl);
  }

  // Aplica SÓ o realce sobre a imagem já recortada (não re-detecta/re-warpa).
  async function aplicarRealce(recortada: string, recortado: boolean, realce: OpcaoRealce) {
    setVista('revisao');
    setRevisao({ recortada, recortado, resultado: null, realce, processando: true });
    try {
      const resultado = await realcarDataUrl(recortada, realce);
      setRevisao({ recortada, recortado, resultado, realce, processando: false });
    } catch {
      // Não conseguiu realçar: usa a própria imagem recortada para não travar.
      setRevisao({ recortada, recortado, resultado: recortada, realce, processando: false });
    }
  }

  // Saída do editor: imagem recortada (warp) ou "página inteira" → revisão de realce.
  function aoRecortar(recortada: string, recortado: boolean) {
    void aplicarRealce(recortada, recortado, 'documento');
  }

  function trocarRealce(realce: OpcaoRealce) {
    if (!revisao) return;
    void aplicarRealce(revisao.recortada, revisao.recortado, realce);
  }

  function aceitarPagina() {
    if (!revisao?.resultado) return;
    setPaginas((atual) => [
      ...atual,
      { id: novoId(), dataUrl: revisao.resultado!, recortado: revisao.recortado },
    ]);
    setRevisao(null);
    setFotoCrua(null);
    setVista('lista');
  }

  function refazerCaptura() {
    setRevisao(null);
    setFotoCrua(null);
    abrirCaptura();
  }

  function removerPagina(id: string) {
    setPaginas((atual) => atual.filter((p) => p.id !== id));
  }

  function moverPagina(id: string, direcao: -1 | 1) {
    setPaginas((atual) => {
      const i = atual.findIndex((p) => p.id === id);
      const j = i + direcao;
      if (i < 0 || j < 0 || j >= atual.length) return atual;
      const copia = [...atual];
      [copia[i], copia[j]] = [copia[j], copia[i]];
      return copia;
    });
  }

  function refazerPagina(id: string) {
    removerPagina(id);
    abrirCaptura();
  }

  // ---- Câmera ao vivo ----------------------------------------------------
  if (vista === 'camera') {
    return <CameraAoVivo aoCapturar={aoCapturarFoto} aoCancelar={() => setVista('lista')} />;
  }

  // ---- Editor de recorte manual (4 cantos) -------------------------------
  if (vista === 'editor' && fotoCrua) {
    return (
      <EditorRecorte
        fotoCrua={fotoCrua}
        aoConfirmar={aoRecortar}
        aoRefazerFoto={refazerCaptura}
        aoCancelar={() => {
          setFotoCrua(null);
          setVista('lista');
        }}
      />
    );
  }

  // ---- Revisão da página (só realce sobre a imagem já recortada) ---------
  if (vista === 'revisao') {
    return (
      <div className="space-y-4">
        <div className="overflow-hidden rounded-2xl border border-areia bg-tinta/5">
          <div className="grid min-h-[320px] place-items-center bg-tinta/90 p-2">
            {revisao?.processando || !revisao?.resultado ? (
              <div className="flex flex-col items-center gap-3 py-12 text-white/90">
                <Spinner className="text-white" />
                <p className="text-sm">Aplicando realce…</p>
              </div>
            ) : (
              <img
                src={revisao.resultado}
                alt="Página digitalizada"
                className="max-h-[60vh] w-auto rounded-lg object-contain"
              />
            )}
          </div>
        </div>

        {revisao && !revisao.processando && revisao.resultado && (
          <p className="text-center text-xs text-tinta-mute">
            {revisao.recortado
              ? 'Recorte aplicado — confira a página antes de usar.'
              : 'Página inteira (sem recorte).'}
          </p>
        )}

        {/* Ajuste: tipo de realce (documento P/B x cor) */}
        <div className="flex gap-2">
          <SegBotao
            ativo={revisao?.realce === 'documento'}
            onClick={() => trocarRealce('documento')}
            disabled={revisao?.processando}
          >
            Documento (P/B)
          </SegBotao>
          <SegBotao
            ativo={revisao?.realce === 'cor'}
            onClick={() => trocarRealce('cor')}
            disabled={revisao?.processando}
          >
            Cor
          </SegBotao>
        </div>

        <div className="flex gap-3">
          <GhostButton className="flex-1" onClick={() => setVista('editor')} disabled={!fotoCrua}>
            <Crop className="h-4 w-4" /> Ajustar recorte
          </GhostButton>
          <PrimaryButton
            className="flex-1"
            onClick={aceitarPagina}
            disabled={revisao?.processando || !revisao?.resultado}
          >
            <Check className="h-5 w-5" /> Usar página
          </PrimaryButton>
        </div>
      </div>
    );
  }

  // ---- Lista de páginas (tela principal do scanner) ----------------------
  return (
    <div className="space-y-5">
      <input
        ref={inputArquivo}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={aoEscolherArquivo}
      />

      <div>
        <p className="font-display text-lg font-semibold text-tinta">Páginas do documento</p>
        <p className="text-sm text-tinta-mute">
          Fotografe cada folha do exame. Você pode reordenar, refazer ou remover antes de enviar.
        </p>
      </div>

      {scannerPronto === false && (
        <p className="rounded-xl bg-areia/50 px-3 py-2 text-xs text-tinta-mute">
          Recorte automático indisponível neste aparelho/rede — as fotos serão enviadas com um
          ajuste de contraste.
        </p>
      )}

      {paginas.length === 0 ? (
        <button
          type="button"
          onClick={abrirCaptura}
          className="flex w-full flex-col items-center gap-3 rounded-2xl border-2 border-dashed border-marica/30 bg-marica/[0.03] px-6 py-12 text-center transition active:scale-[.99]"
        >
          <span className="grid h-16 w-16 place-items-center rounded-2xl bg-marica/10 text-marica">
            <ScanLine className="h-8 w-8" />
          </span>
          <span className="font-display text-base font-semibold text-tinta">Adicionar primeira página</span>
          <span className="text-xs text-tinta-mute">Toque para abrir a câmera traseira</span>
        </button>
      ) : (
        <ul className="space-y-3">
          {paginas.map((p, indice) => (
            <li
              key={p.id}
              className="flex items-center gap-3 rounded-2xl border border-areia bg-white p-3 shadow-carta"
            >
              <span className="relative h-20 w-16 shrink-0 overflow-hidden rounded-lg border border-areia bg-papel">
                <img src={p.dataUrl} alt={`Página ${indice + 1}`} className="h-full w-full object-cover" />
                <span className="absolute left-1 top-1 grid h-5 w-5 place-items-center rounded-full bg-marica text-[11px] font-bold text-white">
                  {indice + 1}
                </span>
              </span>

              <div className="flex flex-1 flex-col gap-1">
                <div className="flex gap-1">
                  <BotaoIcone
                    rotulo="Mover para cima"
                    onClick={() => moverPagina(p.id, -1)}
                    disabled={indice === 0}
                  >
                    <ArrowUp className="h-4 w-4" />
                  </BotaoIcone>
                  <BotaoIcone
                    rotulo="Mover para baixo"
                    onClick={() => moverPagina(p.id, 1)}
                    disabled={indice === paginas.length - 1}
                  >
                    <ArrowDown className="h-4 w-4" />
                  </BotaoIcone>
                  <BotaoIcone rotulo="Refazer página" onClick={() => refazerPagina(p.id)}>
                    <RotateCcw className="h-4 w-4" />
                  </BotaoIcone>
                  <BotaoIcone rotulo="Remover página" onClick={() => removerPagina(p.id)} perigo>
                    <Trash2 className="h-4 w-4" />
                  </BotaoIcone>
                </div>
                <span className="text-[11px] text-tinta-mute">
                  {p.recortado ? 'Recortada' : 'Página inteira'}
                </span>
              </div>
            </li>
          ))}
        </ul>
      )}

      {paginas.length > 0 && (
        <div className="flex gap-3">
          {temCameraAoVivo && (
            <GhostButton className="flex-1" onClick={() => setVista('camera')}>
              <Camera className="h-4 w-4" /> Câmera
            </GhostButton>
          )}
          <GhostButton className="flex-1" onClick={() => inputArquivo.current?.click()}>
            <ImagePlus className="h-4 w-4" /> Tirar foto
          </GhostButton>
        </div>
      )}

      <div className="space-y-3 pt-1">
        <PrimaryButton onClick={() => setConfirmar('concluir')} disabled={paginas.length === 0}>
          <Check className="h-5 w-5" /> Concluir ({paginas.length})
        </PrimaryButton>
        <GhostButton
          className="w-full"
          onClick={() => (paginas.length > 0 ? setConfirmar('fechar') : aoCancelar())}
        >
          Cancelar
        </GhostButton>
      </div>

      {confirmar === 'concluir' && (
        <ConfirmacaoModal
          titulo="Concluir documento?"
          mensagem={`Vamos gerar o PDF com ${paginas.length} página${
            paginas.length > 1 ? 's' : ''
          } e enviar para o profissional.`}
          rotuloConfirmar="Concluir e enviar"
          aoConfirmar={() => {
            setConfirmar(null);
            aoConcluir(paginas.map((p) => p.dataUrl));
          }}
          aoVoltar={() => setConfirmar(null)}
        />
      )}
      {confirmar === 'fechar' && (
        <ConfirmacaoModal
          titulo="Descartar páginas?"
          mensagem={`${paginas.length} página${paginas.length > 1 ? 's' : ''} capturada${
            paginas.length > 1 ? 's serão perdidas' : ' será perdida'
          }. Esta ação não pode ser desfeita.`}
          rotuloConfirmar="Descartar"
          aoConfirmar={() => {
            setConfirmar(null);
            aoCancelar();
          }}
          aoVoltar={() => setConfirmar(null)}
        />
      )}
    </div>
  );
}

/** Modal de confirmação (descartar páginas / concluir documento). */
function ConfirmacaoModal({
  titulo,
  mensagem,
  rotuloConfirmar,
  aoConfirmar,
  aoVoltar,
}: {
  titulo: string;
  mensagem: string;
  rotuloConfirmar: string;
  aoConfirmar: () => void;
  aoVoltar: () => void;
}) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      className="fixed inset-0 z-[60] mx-auto flex max-w-[460px] items-center justify-center bg-tinta/60 p-5"
      onClick={aoVoltar}
    >
      <div
        className="w-full max-w-sm rounded-2xl bg-papel p-5 shadow-carta"
        onClick={(e) => e.stopPropagation()}
      >
        <p className="font-display text-lg font-semibold text-tinta">{titulo}</p>
        <p className="mt-2 text-sm text-tinta-mute">{mensagem}</p>
        <div className="mt-5 flex gap-3">
          <GhostButton className="flex-1" onClick={aoVoltar}>
            Voltar
          </GhostButton>
          <PrimaryButton className="flex-1" onClick={aoConfirmar}>
            {rotuloConfirmar}
          </PrimaryButton>
        </div>
      </div>
    </div>
  );
}

/** Botãozinho de ação na lista de páginas. */
function BotaoIcone({
  children,
  rotulo,
  onClick,
  disabled,
  perigo,
}: {
  children: React.ReactNode;
  rotulo: string;
  onClick: () => void;
  disabled?: boolean;
  perigo?: boolean;
}) {
  return (
    <button
      type="button"
      aria-label={rotulo}
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'grid h-10 w-10 place-items-center rounded-xl border border-areia bg-white transition active:scale-95',
        'disabled:pointer-events-none disabled:opacity-40',
        perigo ? 'text-marica active:bg-marica/10' : 'text-tinta active:bg-areia/60',
      )}
    >
      {children}
    </button>
  );
}

/** Botão segmentado (escolha de realce). */
function SegBotao({
  children,
  ativo,
  onClick,
  disabled,
}: {
  children: React.ReactNode;
  ativo?: boolean;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'flex-1 rounded-2xl border px-3 py-2.5 text-sm font-semibold transition active:scale-[.99]',
        'disabled:pointer-events-none disabled:opacity-50',
        ativo ? 'border-marica bg-marica/10 text-marica' : 'border-areia bg-white text-tinta',
      )}
    >
      {children}
    </button>
  );
}

/**
 * Preview ao vivo da câmera traseira (getUserMedia). Mostra um disparador grande.
 * Se a câmera não abrir, orienta o uso do fallback (botão "Tirar foto" na lista).
 */
function CameraAoVivo({
  aoCapturar,
  aoCancelar,
}: {
  aoCapturar: (dataUrl: string) => void;
  aoCancelar: () => void;
}) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let stream: MediaStream | null = null;
    let cancelado = false;

    (async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({
          video: {
            facingMode: { ideal: 'environment' },
            // Pede 4K p/ documento (mais resolução = texto legível); o navegador cai
            // para a maior resolução suportada se o aparelho não tiver 4K.
            width: { ideal: 3840 },
            height: { ideal: 2160 },
          },
          audio: false,
        });
        if (cancelado) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          try {
            await videoRef.current.play();
          } catch {
            /* play() pode ser interrompido ao desmontar — ignorar */
          }
        }
      } catch (err) {
        const negada = (err as DOMException | undefined)?.name === 'NotAllowedError';
        setErro(
          negada
            ? 'Permissão de câmera negada. Libere a câmera nas configurações ou use "Tirar foto".'
            : 'Não foi possível abrir a câmera. Use o botão "Tirar foto".',
        );
      }
    })();

    return () => {
      cancelado = true;
      stream?.getTracks().forEach((t) => t.stop());
    };
  }, []);

  function disparar() {
    const v = videoRef.current;
    if (!v || !v.videoWidth) return;
    const canvas = document.createElement('canvas');
    canvas.width = v.videoWidth;
    canvas.height = v.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(v, 0, 0);
    aoCapturar(canvas.toDataURL('image/jpeg', 0.92));
  }

  return (
    <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] flex-col bg-black">
      <div className="flex items-center justify-between px-3 pb-2 pt-[calc(env(safe-area-inset-top)+0.75rem)]">
        <button
          type="button"
          onClick={aoCancelar}
          aria-label="Fechar câmera"
          className="grid h-11 w-11 place-items-center rounded-xl text-white transition active:bg-white/15"
        >
          <X className="h-6 w-6" />
        </button>
        <span className="text-sm font-medium text-white/80">Enquadre o documento</span>
        <span className="h-11 w-11" />
      </div>

      <div className="relative flex-1 overflow-hidden">
        <video
          ref={videoRef}
          playsInline
          muted
          autoPlay
          className="h-full w-full object-cover"
        />
        {/* Moldura-guia para o enquadramento. */}
        <div className="pointer-events-none absolute inset-6 rounded-2xl border-2 border-white/50" />
        {erro && (
          <div className="absolute inset-x-4 top-1/2 -translate-y-1/2 rounded-2xl bg-black/70 p-4 text-center text-sm text-white">
            {erro}
          </div>
        )}
      </div>

      <div className="flex items-center justify-center px-6 pb-[calc(env(safe-area-inset-bottom)+1.5rem)] pt-4">
        <button
          type="button"
          onClick={disparar}
          aria-label="Capturar"
          disabled={!!erro}
          className="grid h-18 w-18 place-items-center rounded-full bg-white ring-4 ring-white/40 transition active:scale-95 disabled:opacity-40"
          style={{ height: 72, width: 72 }}
        >
          <span className="grid h-14 w-14 place-items-center rounded-full bg-marica text-white">
            <Camera className="h-7 w-7" />
          </span>
        </button>
      </div>
    </div>
  );
}
