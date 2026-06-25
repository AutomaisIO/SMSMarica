// jscanify não traz tipos próprios — tratamos como `any`. A API usada
// (findPaperContour / getCornerPoints / extractPaper) está em `@/lib/scanner`.
// Usamos o subpath `/client` (build de browser; o `main` é Node-only: canvas+jsdom).
declare module 'jscanify/client';

// OpenCV.js é carregado em runtime via <script> de CDN e exposto em `window.cv`.
interface Window {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  cv: any;
}
