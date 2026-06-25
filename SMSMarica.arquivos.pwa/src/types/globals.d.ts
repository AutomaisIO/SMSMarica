// jscanify não traz tipos próprios — tratamos como `any`. A API usada
// (findPaperContour / getCornerPoints / extractPaper) está em `@/lib/scanner`.
// Usamos o subpath `/client` (build de browser; o `main` é Node-only: canvas+jsdom).
declare module 'jscanify/client';

// OpenCV.js é carregado em runtime via <script> de CDN e exposto em `window.cv`.
interface Window {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  cv: any;
  // BarcodeDetector: nativo no Android Chrome (leitura de QR rápida). NÃO existe no
  // iOS Safari — por isso o LeitorQr cai para jsQR quando esta API está ausente.
  BarcodeDetector?: BarcodeDetectorCtor;
}

/** Subconjunto da Barcode Detection API que usamos (só o `rawValue` do QR). */
interface DetectedBarcodeLike {
  rawValue: string;
}
interface BarcodeDetectorLike {
  detect(source: CanvasImageSource): Promise<DetectedBarcodeLike[]>;
}
interface BarcodeDetectorCtor {
  new (options?: { formats?: string[] }): BarcodeDetectorLike;
  getSupportedFormats?: () => Promise<string[]>;
}
