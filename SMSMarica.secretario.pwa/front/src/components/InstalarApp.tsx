import { useEffect, useState } from 'react';
import { Share, Plus, Smartphone, X } from 'lucide-react';

/** Evento não-padronizado disparado por Chrome/Edge/Android antes de oferecer a instalação. */
type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
};

// Dispensar não some para sempre: adia por alguns dias e o convite volta.
const CHAVE_ADIADO_ATE = 'painel.instalar.adiado_ate';
const DIAS_ADIAR = 7;

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

/** Já roda como app instalado — nada a oferecer. */
function jaInstalado(): boolean {
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    // Safari iOS expõe esta flag não-padronizada.
    (navigator as unknown as { standalone?: boolean }).standalone === true
  );
}

/**
 * Convite para instalar o painel na tela inicial.
 *
 * Diferente do app do cidadão, aqui o iPhone TAMBÉM é convidado: o painel é
 * público (sem login), então não há sessão para se perder no armazenamento
 * isolado do app instalado — e é justamente no celular que o Secretário abre.
 * Como o Safari não expõe `beforeinstallprompt`, no iOS mostramos o passo a passo.
 */
export function InstalarApp() {
  const [deferido, setDeferido] = useState<BeforeInstallPromptEvent | null>(null);
  const [oculto, setOculto] = useState(() => jaInstalado() || estaAdiado());
  const [passosIos, setPassosIos] = useState(false);
  const ios = ehIos();

  useEffect(() => {
    if (oculto) return;

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

  // Android/Chrome só aparece quando o navegador confirma que dá para instalar.
  const podeOferecer = !oculto && (ios || deferido !== null);
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
    <div className="overflow-hidden rounded-2xl border border-vermelho-marica/25 bg-vermelho-marica/[0.05] shadow-cartao">
      {/* flex-wrap + min-w no texto: no celular os botões descem para a própria
          linha em vez de espremer a frase em uma palavra por linha. */}
      <div className="flex flex-wrap items-center gap-x-3 gap-y-3 p-4">
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-vermelho-marica text-white">
          <Smartphone className="h-5 w-5" aria-hidden="true" />
        </span>
        <div className="min-w-[180px] flex-1">
          <p className="font-display text-[15px] font-bold leading-tight text-tinta">
            Tenha o painel na tela inicial
          </p>
          <p className="mt-0.5 text-[13px] leading-snug text-grafite">
            Abre em tela cheia, direto no ícone — sem procurar o endereço no navegador.
          </p>
        </div>
        <div className="ml-auto flex shrink-0 items-center gap-2">
          {ios ? (
            <button
              type="button"
              onClick={() => setPassosIos((v) => !v)}
              aria-expanded={passosIos}
              className="rounded-xl bg-vermelho-marica px-3.5 py-2 text-[13.5px] font-semibold text-white transition active:scale-95"
            >
              {passosIos ? 'Fechar' : 'Como instalar'}
            </button>
          ) : (
            <button
              type="button"
              onClick={instalar}
              className="rounded-xl bg-vermelho-marica px-3.5 py-2 text-[13.5px] font-semibold text-white transition active:scale-95"
            >
              Instalar
            </button>
          )}
          <button
            type="button"
            onClick={dispensar}
            aria-label="Dispensar por uma semana"
            className="grid h-8 w-8 place-items-center rounded-lg text-grafite transition hover:bg-vermelho-marica/10"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      </div>

      {ios && passosIos && (
        <ol className="space-y-2 border-t border-vermelho-marica/15 bg-papel px-4 py-3.5 text-[13.5px] text-tinta">
          <li className="flex items-center gap-2.5">
            <Share className="h-4 w-4 shrink-0 text-vermelho-marica" aria-hidden="true" />
            Toque em <strong className="font-semibold">Compartilhar</strong>, na barra do Safari.
          </li>
          <li className="flex items-center gap-2.5">
            <Plus className="h-4 w-4 shrink-0 text-vermelho-marica" aria-hidden="true" />
            Escolha <strong className="font-semibold">Adicionar à Tela de Início</strong>.
          </li>
          <li className="flex items-center gap-2.5">
            <span
              className="grid h-4 w-4 shrink-0 place-items-center rounded-[3px] bg-vermelho-marica text-[10px] font-bold text-white"
              aria-hidden="true"
            >
              ✓
            </span>
            Confirme em <strong className="font-semibold">Adicionar</strong>.
          </li>
        </ol>
      )}
    </div>
  );
}
