import { create } from 'zustand';

export type EstadoRegistro = 'desligado' | 'outraAba' | 'conectando' | 'registrado' | 'falhou';

export type Chamada = {
  direcao: 'entrada' | 'saida';
  numero: string;
  nome: string | null;
  /** tocando = entrada aguardando atender; chamando = saída aguardando a outra ponta. */
  fase: 'tocando' | 'chamando' | 'emCurso';
  inicio: number | null;
  mudo: boolean;
  emEspera: boolean;
};

type Estado = {
  registro: EstadoRegistro;
  mensagem: string | null;
  ramal: string | null;
  chamada: Chamada | null;
  definirRegistro: (registro: EstadoRegistro, mensagem?: string | null) => void;
  definirRamal: (ramal: string | null) => void;
  definirChamada: (chamada: Chamada | null) => void;
  atualizarChamada: (parcial: Partial<Chamada>) => void;
};

/** Estado visível do softphone. Quem o altera é o motor (lib/motorSoftphone). */
export const useSoftphone = create<Estado>((set) => ({
  registro: 'desligado',
  mensagem: null,
  ramal: null,
  chamada: null,
  definirRegistro: (registro, mensagem = null) => set({ registro, mensagem }),
  definirRamal: (ramal) => set({ ramal }),
  definirChamada: (chamada) => set({ chamada }),
  atualizarChamada: (parcial) => set((s) => (s.chamada ? { chamada: { ...s.chamada, ...parcial } } : s)),
}));
