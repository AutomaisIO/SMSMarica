export const temaMarica = {
  cores: {
    primaria: '#C8102E',
    primariaClara: '#E03C52',
    primariaEscura: '#A80C27',
    neutra: '#FFFFFF',
    texto: '#0F172A',
    fundo: '#F8FAFC',
    borda: '#E2E8F0',
  },
  tipografia: {
    familia: 'Inter, system-ui, sans-serif',
  },
} as const;

export type TemaMarica = typeof temaMarica;
