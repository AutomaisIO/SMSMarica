import { create } from 'zustand';

type PdfViewerState = {
  aberto: boolean;
  dados: ArrayBuffer | null;
  nome: string;
  abrir: (dados: ArrayBuffer, nome: string) => void;
  fechar: () => void;
};

/**
 * Visualizador de PDF embutido no app — evita depender do leitor de PDF do celular
 * (muitos idosos não têm um instalado). O `abrirPdf` (lib/pdf) empurra os bytes aqui e
 * o <VisualizadorPdf/> (montado no AppShell) renderiza com pdf.js.
 */
export const usePdfViewer = create<PdfViewerState>((set) => ({
  aberto: false,
  dados: null,
  nome: 'documento.pdf',
  abrir: (dados, nome) => set({ aberto: true, dados, nome }),
  fechar: () => set({ aberto: false, dados: null }),
}));
