import { useEffect, useState } from 'react';
import { Download, Plus, Share, X } from 'lucide-react';
import { PrimaryButton } from '@/components/ui';

/** Evento não-padronizado disparado por Chrome/Edge/Android antes de oferecer a instalação. */
type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
};

const CHAVE_DISPENSADO = 'sms.instalar.dispensado';

/** iPhone/iPad (iPadOS 13+ se disfarça de Mac com toque). */
function ehIos(): boolean {
  const ua = navigator.userAgent;
  const iphone = /iPad|iPhone|iPod/.test(ua);
  const ipad = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
  return iphone || ipad;
}

/** Já está rodando como app instalado (standalone) — nada a oferecer. */
function jaInstalado(): boolean {
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    // Safari iOS expõe esta flag não-padronizada.
    (navigator as unknown as { standalone?: boolean }).standalone === true
  );
}

export function InstalarApp() {
  const [deferido, setDeferido] = useState<BeforeInstallPromptEvent | null>(null);
  const [mostrarIos, setMostrarIos] = useState(false);
  const [oculto, setOculto] = useState(
    () => jaInstalado() || localStorage.getItem(CHAVE_DISPENSADO) === '1',
  );

  useEffect(() => {
    if (oculto) return;

    // Android/Chrome: capturamos o evento para disparar a instalação sob demanda.
    function aoPrompt(e: Event) {
      e.preventDefault();
      setDeferido(e as BeforeInstallPromptEvent);
    }
    function aoInstalar() {
      setOculto(true);
    }
    window.addEventListener('beforeinstallprompt', aoPrompt);
    window.addEventListener('appinstalled', aoInstalar);
    return () => {
      window.removeEventListener('beforeinstallprompt', aoPrompt);
      window.removeEventListener('appinstalled', aoInstalar);
    };
  }, [oculto]);

  // No iOS não existe beforeinstallprompt: o banner aparece direto com as instruções.
  const ios = ehIos();
  const podeOferecer = !oculto && (deferido !== null || ios);
  if (!podeOferecer) return null;

  function dispensar() {
    localStorage.setItem(CHAVE_DISPENSADO, '1');
    setOculto(true);
  }

  async function instalar() {
    if (ios) {
      setMostrarIos(true);
      return;
    }
    if (!deferido) return;
    await deferido.prompt();
    const { outcome } = await deferido.userChoice;
    setDeferido(null);
    if (outcome === 'accepted') setOculto(true);
  }

  return (
    <>
      <div className="flex items-center gap-3 rounded-2xl border border-marica/20 bg-marica/[0.04] p-3.5">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-marica/10 text-marica">
          <Download className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-display text-sm font-semibold text-tinta">Instale o app no celular</p>
          <p className="text-xs text-tinta-mute">Acesso rápido, em tela cheia, direto na sua tela inicial.</p>
        </div>
        <button
          type="button"
          onClick={instalar}
          className="shrink-0 rounded-xl bg-marica px-3.5 py-2 text-sm font-semibold text-white transition active:scale-95 active:bg-marica-escuro"
        >
          Instalar
        </button>
        <button
          type="button"
          onClick={dispensar}
          aria-label="Dispensar"
          className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-tinta-mute transition active:bg-areia/60"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      {/* iOS: passo a passo do "Adicionar à Tela de Início" (só funciona no Safari). */}
      {mostrarIos && (
        <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] items-end" role="dialog" aria-modal="true">
          <button
            type="button"
            aria-label="Fechar"
            onClick={() => setMostrarIos(false)}
            className="absolute inset-0 animate-fade-in bg-tinta/40 backdrop-blur-[1px]"
          />
          <div className="relative w-full animate-rise rounded-t-3xl bg-papel p-6 shadow-2xl">
            <div className="mb-4 flex items-start justify-between">
              <div>
                <p className="font-display text-lg font-semibold text-tinta">Instalar no iPhone</p>
                <p className="text-sm text-tinta-mute">Pelo Safari, em 3 passos:</p>
              </div>
              <button
                type="button"
                onClick={() => setMostrarIos(false)}
                aria-label="Fechar"
                className="grid h-9 w-9 place-items-center rounded-lg text-tinta-mute transition active:bg-areia/60"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <ol className="space-y-4">
              <li className="flex items-center gap-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">1</span>
                <span className="flex flex-wrap items-center gap-1 text-sm text-tinta">
                  Toque em <Share className="h-4 w-4 text-lagoa" /> <strong>Compartilhar</strong>, na barra do Safari.
                </span>
              </li>
              <li className="flex items-center gap-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">2</span>
                <span className="flex flex-wrap items-center gap-1 text-sm text-tinta">
                  Escolha <Plus className="h-4 w-4 text-lagoa" /> <strong>Adicionar à Tela de Início</strong>.
                </span>
              </li>
              <li className="flex items-center gap-3">
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-marica/10 font-display text-sm font-bold text-marica">3</span>
                <span className="text-sm text-tinta">
                  Confirme em <strong>Adicionar</strong>. Pronto — o ícone do SMS Cidadão aparece na sua tela.
                </span>
              </li>
            </ol>

            <p className="mt-5 rounded-xl bg-areia/50 px-3 py-2 text-xs text-tinta-mute">
              Não vê o botão Compartilhar? Abra o site pelo <strong>Safari</strong> — outros navegadores no iPhone não
              permitem instalar.
            </p>

            <PrimaryButton className="mt-5" onClick={() => setMostrarIos(false)}>
              Entendi
            </PrimaryButton>
          </div>
        </div>
      )}
    </>
  );
}
