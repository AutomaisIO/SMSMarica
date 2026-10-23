import { useEffect, useRef } from 'react';
import { MessagesSquare, X } from 'lucide-react';
import { useTemConsulta } from '@/shared/auth/authStore';
import { cn } from '@/shared/lib/cn';
import { useResumoInterno } from '@/features/comunicacao-interna/api/queries';
import { useInternoHub } from '@/features/comunicacao-interna/hooks/useInternoHub';
import { useInterno } from '@/features/comunicacao-interna/store/internoStore';
import { PainelInterno } from '@/features/comunicacao-interna/components/PainelInterno';

/** "Pulo" do painel quando ele abre sozinho (ou chega mensagem com ele aberto). */
const PULO: Keyframe[] = [
  { transform: 'translateY(0) scale(1)' },
  { transform: 'translateY(-18px) scale(1.01)', offset: 0.3 },
  { transform: 'translateY(0) scale(1)', offset: 0.55 },
  { transform: 'translateY(-6px)', offset: 0.75 },
  { transform: 'translateY(0)' },
];

/** Balão fechado: chacoalha para chamar o olho sem abrir nada. */
const CHACOALHO: Keyframe[] = [
  { transform: 'rotate(0)' },
  { transform: 'rotate(-14deg)' },
  { transform: 'rotate(12deg)' },
  { transform: 'rotate(-8deg)' },
  { transform: 'rotate(5deg)' },
  { transform: 'rotate(0)' },
];

/**
 * Balão LARANJA da Comunicação interna (ADR-0079), montado no Layout: fica ACIMA do balão da
 * Central de Atendimento (ou no lugar dele, para quem não tem a Central) e abre o painel encaixado
 * no canto. Mantém o hub ligado em qualquer tela — é ele que faz o painel "pular" quando chega
 * mensagem, toca o som e manda a notificação do Windows.
 */
export function BalaoInterno() {
  const podeVer = useTemConsulta('ComunicacaoInterna');
  const temCentral = useTemConsulta('Conversas');
  const painelAberto = useInterno((s) => s.painelAberto);
  const total = useInterno((s) => s.totalNaoLidas);
  const pulo = useInterno((s) => s.pulo);
  const painelRef = useRef<HTMLDivElement>(null);
  const balaoRef = useRef<HTMLButtonElement>(null);

  // Hub e resumo sempre ativos enquanto logado (antes do early-return).
  useInternoHub(podeVer);
  useResumoInterno(podeVer);

  useEffect(() => {
    if (pulo === 0) return;
    const alvo = useInterno.getState().painelAberto ? painelRef.current : balaoRef.current;
    if (!alvo || typeof alvo.animate !== 'function') return;
    const aberto = alvo === painelRef.current;
    alvo.animate(aberto ? PULO : CHACOALHO, { duration: aberto ? 650 : 550, easing: 'ease-out' });
  }, [pulo]);

  if (!podeVer) return null;

  return (
    <>
      {painelAberto && (
        <div
          ref={painelRef}
          // Qualquer gesto dentro do painel = a pessoa assumiu: o composer volta a pegar o foco.
          onPointerDown={() => useInterno.getState().confirmarGesto()}
          className={cn(
            'fixed inset-0 z-50 overflow-hidden bg-white shadow-2xl',
            'sm:inset-auto sm:bottom-4 sm:right-24 sm:h-[min(560px,calc(100vh-2rem))] sm:w-[min(760px,calc(100vw-7.5rem))] sm:rounded-xl sm:ring-1 sm:ring-black/10',
          )}
          role="dialog"
          aria-label="Comunicação interna"
        >
          <PainelInterno />
        </div>
      )}

      <button
        ref={balaoRef}
        type="button"
        onClick={() => {
          const s = useInterno.getState();
          if (s.painelAberto) s.fecharPainel();
          else s.abrirPainel();
        }}
        className={cn(
          'fixed right-4 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-orange-500 text-white shadow-lg hover:bg-orange-600',
          temCentral ? 'bottom-[5.5rem]' : 'bottom-4',
          painelAberto && 'max-sm:hidden',
        )}
        title={painelAberto ? 'Minimizar a Comunicação interna' : 'Comunicação interna — conversar com outras unidades'}
        aria-label="Comunicação interna"
      >
        {painelAberto ? <X className="h-6 w-6" /> : <MessagesSquare className="h-6 w-6" />}
        {total > 0 && (
          <span className="absolute -right-0.5 -top-0.5 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-red-600 px-1 text-[11px] font-bold text-white ring-2 ring-white">
            {total > 99 ? '99+' : total}
          </span>
        )}
      </button>
    </>
  );
}
