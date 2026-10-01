import { create } from 'zustand';

type PdfViewerState = {
  aberto: boolean;
  dados: ArrayBuffer | null;
  nome: string;
  /** 'application/pdf' (pdf.js) ou 'image/*' (foto enviada/recebida — exibida como <img>). */
  mimeType: string;
  abrir: (dados: ArrayBuffer, nome: string, mimeType?: string) => void;
  fechar: () => void;
};

/**
 * Visualizador de documentos embutido no app — evita depender do leitor de PDF do celular
 * (muitos idosos não têm um instalado). O `abrirPdf`/`abrirDocumento` (lib/pdf) empurra os
 * bytes aqui e o <VisualizadorPdf/> (montado no AppShell) renderiza: PDF com pdf.js, foto
 * como imagem — mesmo zoom, mesmo Baixar/Compartilhar.
 */
export const usePdfViewer = create<PdfViewerState>((set) => ({
  aberto: false,
  dados: null,
  nome: 'documento.pdf',
  mimeType: 'application/pdf',
  abrir: (dados, nome, mimeType = 'application/pdf') => set({ aberto: true, dados, nome, mimeType }),
  fechar: () => set({ aberto: false, dados: null }),
}));
