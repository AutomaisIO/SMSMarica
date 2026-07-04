import { useEffect, useState } from 'react';
import { Download, X } from 'lucide-react';

/** Evento não-padronizado disparado por Chrome/Edge/Android antes de oferecer a instalação. */
type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
};

// Dispensar não some para sempre: adia por alguns dias e o convite volta a aparecer.
const CHAVE_ADIADO_ATE = 'sms.instalar.adiado_ate';
const DIAS_ADIAR = 4;

function estaAdiado(): boolean {
  const ate = Number(localStorage.getItem(CHAVE_ADIADO_ATE) ?? 0);
  return Number.isFinite(ate) && Date.now() < ate;
}

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
  const [oculto, setOculto] = useState(() => jaInstalado() || estaAdiado());

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

  // No iOS NÃO sugerimos instalar: o app instalado tem armazenamento isolado (abre deslogado)
  // e o magic link do WhatsApp sempre abre no Safari — instalar só confunde o público idoso.
  // A entrada no iPhone é sempre o link → Safari (já logado). Só oferecemos no Android/Chrome.
  if (ehIos()) return null;
  const podeOferecer = !oculto && deferido !== null;
  if (!podeOferecer) return null;

  function dispensar() {
    localStorage.setItem(CHAVE_ADIADO_ATE, String(Date.now() + DIAS_ADIAR * 24 * 60 * 60 * 1000));
    setOculto(true);
  }

  async function instalar() {
    if (!deferido) return;
    await deferido.prompt();
    const { outcome } = await deferido.userChoice;
    setDeferido(null);
    if (outcome === 'accepted') setOculto(true);
  }

  return (
    <div className="flex items-center gap-3 rounded-2xl border border-marica/20 bg-marica/[0.04] p-3.5">
      <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-marica/10 text-marica">
        <Download className="h-5 w-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-display text-sm font-semibold text-tinta">Instale o Saúde Maricá</p>
        <p className="text-xs text-tinta-mute">
          Abre em tela cheia, com o ícone do app na tela inicial — sem passar pelo navegador.
        </p>
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
  );
}
