import { create } from 'zustand';
import { salvarPreferencias } from '@/shared/auth/preferenciasApi';

/**
 * Preferências da caixa de digitação do chat:
 *  - altura (px) do textarea, ajustável pela alça de redimensionamento;
 *  - se Enter envia a mensagem (Shift+Enter quebra linha) ou se Enter também quebra linha.
 *
 * Fonte da verdade é o servidor (por usuário, acompanha em qualquer browser/PC). O
 * localStorage é só um cache para resposta imediata antes da hidratação chegar.
 */

const CHAVE = 'smsmarica.chat.composer';

export const ALTURA_MIN = 56;
export const ALTURA_MAX = 320;
export const ALTURA_PADRAO = 64;

type Cache = { altura?: number; enviarComEnter?: boolean };

function clampAltura(px: number): number {
  return Math.min(ALTURA_MAX, Math.max(ALTURA_MIN, Math.round(px)));
}

function carregar(): Cache {
  try {
    const raw = localStorage.getItem(CHAVE);
    if (!raw) return {};
    const obj = JSON.parse(raw) as Cache;
    return obj && typeof obj === 'object' ? obj : {};
  } catch {
    return {};
  }
}

function cachearLocal(cache: Cache) {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(cache));
  } catch {
    /* quota/serialização — cache é best-effort. */
  }
}

type Estado = {
  altura: number;
  enviarComEnter: boolean;
  /** Redefine a altura (px) da caixa após o usuário arrastar a alça. */
  definirAltura: (px: number) => void;
  /** Liga/desliga o envio com Enter. */
  definirEnviarComEnter: (v: boolean) => void;
  /** Substitui o estado pelos valores vindos do servidor (hidratação no login). */
  hidratar: (p: Cache) => void;
};

const inicial = carregar();

export const useComposerPreferencias = create<Estado>((set, get) => ({
  altura: inicial.altura ?? ALTURA_PADRAO,
  enviarComEnter: inicial.enviarComEnter ?? true,
  definirAltura: (px) => {
    const altura = clampAltura(px);
    if (altura === get().altura) return;
    set({ altura });
    cachearLocal({ altura, enviarComEnter: get().enviarComEnter });
    // Fire-and-forget: a UI já refletiu; falha de rede não trava nada.
    void salvarPreferencias({ alturaComposerChat: altura }).catch(() => {});
  },
  definirEnviarComEnter: (v) => {
    if (v === get().enviarComEnter) return;
    set({ enviarComEnter: v });
    cachearLocal({ altura: get().altura, enviarComEnter: v });
    void salvarPreferencias({ enviarComEnter: v }).catch(() => {});
  },
  hidratar: ({ altura, enviarComEnter }) => {
    const prox: Estado = { ...get() };
    if (typeof altura === 'number') prox.altura = clampAltura(altura);
    if (typeof enviarComEnter === 'boolean') prox.enviarComEnter = enviarComEnter;
    cachearLocal({ altura: prox.altura, enviarComEnter: prox.enviarComEnter });
    set({ altura: prox.altura, enviarComEnter: prox.enviarComEnter });
  },
}));
