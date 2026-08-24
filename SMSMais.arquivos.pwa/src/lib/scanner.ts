/**
 * Núcleo "CamScanner" do app, 100% no cliente.
 *
 * Pipeline por página:
 *   1. jscanify (sobre OpenCV.js) detecta o quadrilátero do papel na foto;
 *   2. recorta + corrige a perspectiva para um retângulo (warp);
 *   3. realça via canvas (tons de cinza + contraste, ou contraste em cor).
 *
 * Degradação graciosa: se o OpenCV/jscanify não carregar (rede ruim, navegador
 * antigo), NÃO falhamos — devolvemos a foto crua com um passe de contraste no
 * canvas e deixamos o usuário seguir. O recorte é um "plus", não um pré-requisito.
 *
 * OpenCV.js é pesado (~8 MB WASM) e só é necessário ao digitalizar, então é
 * carregado sob demanda de uma CDN (lazy-load) com estado de carregamento na UI.
 */

import { ordenarCantos, type Cantos } from './perspectiva';

// Maior lado da imagem processada. Mais alto que antes para preservar a resolução
// (a foto perdia muita nitidez no downscale); o warp gera A4 a ~210 dpi.
const MAX_LADO = 2600;

// CDN do OpenCV.js. docs.opencv.org publica o build oficial pré-compilado.
const OPENCV_CDN = 'https://docs.opencv.org/4.10.0/opencv.js';

// Timeout de segurança: se o OpenCV não ficar pronto, a UI degrada em vez de travar.
const OPENCV_TIMEOUT_MS = 30_000;

export type OpcaoRealce = 'documento' | 'cor';

export type PaginaProcessada = {
  /** dataURL JPEG da página já recortada/realçada. */
  dataUrl: string;
  /** `true` se a borda do papel foi detectada e a perspectiva corrigida. */
  recortado: boolean;
};

type Ponto = { x: number; y: number };
type CantosPapel = {
  topLeftCorner: Ponto;
  topRightCorner: Ponto;
  bottomLeftCorner: Ponto;
  bottomRightCorner: Ponto;
};

/** Subconjunto da API do jscanify que usamos. */
type Jscanify = {
  findPaperContour: (mat: unknown) => unknown;
  getCornerPoints: (contorno: unknown) => CantosPapel | null;
  extractPaper: (
    img: HTMLCanvasElement,
    largura: number,
    altura: number,
    cantos: CantosPapel,
  ) => HTMLCanvasElement;
};

let opencvPromise: Promise<void> | null = null;
let jscanifyPromise: Promise<unknown> | null = null;

/** Carrega o OpenCV.js (uma vez). Resolve quando `cv.Mat` está disponível. */
export function carregarOpenCv(): Promise<void> {
  if (opencvPromise) return opencvPromise;

  opencvPromise = new Promise<void>((resolve, reject) => {
    if (window.cv?.Mat) {
      resolve();
      return;
    }

    const limpar = (timer: number) => window.clearTimeout(timer);
    const timer = window.setTimeout(() => {
      reject(new Error('Tempo esgotado ao carregar o OpenCV.'));
    }, OPENCV_TIMEOUT_MS);

    const script = document.createElement('script');
    script.src = OPENCV_CDN;
    script.async = true;
    script.onload = () => {
      const cv = window.cv;
      if (!cv) {
        limpar(timer);
        reject(new Error('OpenCV não inicializou.'));
        return;
      }
      // Builds variam: já pronto, Promise (módulo) ou Emscripten (onRuntimeInitialized).
      if (cv.Mat) {
        limpar(timer);
        resolve();
      } else if (typeof cv.then === 'function') {
        cv.then((mod: unknown) => {
          window.cv = mod;
          limpar(timer);
          resolve();
        }).catch((e: unknown) => {
          limpar(timer);
          reject(e instanceof Error ? e : new Error('Falha no OpenCV.'));
        });
      } else {
        cv.onRuntimeInitialized = () => {
          limpar(timer);
          resolve();
        };
      }
    };
    script.onerror = () => {
      limpar(timer);
      reject(new Error('Falha ao baixar o OpenCV.'));
    };
    document.head.appendChild(script);
  });

  return opencvPromise;
}

/**
 * Garante OpenCV + jscanify prontos e devolve a instância do scanner.
 * jscanify é importado SÓ depois do OpenCV (ele usa o `cv` global).
 */
async function carregarScanner(): Promise<Jscanify> {
  await carregarOpenCv();
  if (!jscanifyPromise) {
    // Subpath `/client`: build de browser (UMD que usa o `cv` global). O entry
    // padrão do pacote é Node-only (depende de `canvas`/`jsdom`).
    jscanifyPromise = import('jscanify/client').then((mod) => {
      const Construtor = (mod as { default?: new () => Jscanify }).default ??
        (mod as unknown as new () => Jscanify);
      return new Construtor();
    });
  }
  return jscanifyPromise as Promise<Jscanify>;
}

/** Pré-aquece o download do OpenCV. Resolve `true` se ficou pronto, `false` se não. */
export async function prepararScanner(): Promise<boolean> {
  try {
    await carregarOpenCv();
    return true;
  } catch {
    return false;
  }
}

/**
 * Processa uma página: detecta papel + warp (se possível) e realça.
 * `fonte` é a imagem capturada (frame da câmera ou arquivo escolhido).
 */
export async function processarPagina(
  fonte: HTMLImageElement | HTMLCanvasElement,
  realce: OpcaoRealce,
): Promise<PaginaProcessada> {
  const base = paraCanvas(fonte, MAX_LADO);
  let alvo = base;
  let recortado = false;

  try {
    const scanner = await carregarScanner();
    const recorte = recortarPapel(scanner, base);
    if (recorte) {
      alvo = recorte;
      recortado = true;
    }
  } catch {
    // OpenCV/jscanify indisponível — segue com a foto crua (só realce no canvas).
  }

  const final = realce === 'cor' ? realcarCor(alvo) : realcarDocumento(alvo);
  return { dataUrl: final.toDataURL('image/jpeg', 0.85), recortado };
}

/**
 * Detecta os 4 cantos do papel na foto (jscanify sobre OpenCV) para PRÉ-PREENCHER
 * o editor de recorte manual. Devolve os cantos já ordenados (tl/tr/br/bl) em
 * coordenadas de `canvas`, ou `null` se não houver detecção confiável (o editor
 * então usa margens internas padrão). NÃO faz warp — só localiza os cantos.
 */
export async function detectarCantos(canvas: HTMLCanvasElement): Promise<Cantos | null> {
  let scanner: Jscanify;
  try {
    scanner = await carregarScanner();
  } catch {
    return null; // OpenCV/jscanify indisponível — sem pré-detecção.
  }

  const cv = window.cv;
  const mat = cv.imread(canvas);
  let contorno: unknown = null;
  try {
    contorno = scanner.findPaperContour(mat);
    if (!contorno) return null;

    const cantos = scanner.getCornerPoints(contorno);
    if (
      !cantos?.topLeftCorner ||
      !cantos.topRightCorner ||
      !cantos.bottomLeftCorner ||
      !cantos.bottomRightCorner
    ) {
      return null;
    }

    const { topLeftCorner: tl, topRightCorner: tr, bottomLeftCorner: bl, bottomRightCorner: br } = cantos;
    const largura = Math.max(distancia(tl, tr), distancia(bl, br));
    const altura = Math.max(distancia(tl, bl), distancia(tr, br));

    // Detecção minúscula = provável ruído; melhor deixar as margens padrão do editor.
    if (largura < 96 || altura < 96) return null;
    if (largura * altura < canvas.width * canvas.height * 0.12) return null;

    return ordenarCantos([tl, tr, bl, br]);
  } catch {
    return null;
  } finally {
    (contorno as { delete?: () => void } | null)?.delete?.();
    mat.delete();
  }
}

/**
 * Aplica SÓ o realce (contraste/cinza) sobre uma imagem JÁ recortada — sem detecção
 * de bordas nem warp. Usado na etapa de revisão do scanner, depois do recorte manual.
 */
export async function realcarDataUrl(dataUrl: string, realce: OpcaoRealce): Promise<string> {
  const img = await carregarImagem(dataUrl);
  // Não reduz a imagem JÁ recortada (A4 ~2480px ≤ MAX_LADO): só limita a "página inteira".
  const canvas = paraCanvas(img, MAX_LADO);
  const final = realce === 'cor' ? realcarCor(canvas) : realcarDocumento(canvas);
  // Qualidade alta no único passe de JPEG (o recorte intermediário é quase sem perda).
  return final.toDataURL('image/jpeg', 0.92);
}

/** Tenta detectar a borda do papel e devolver o recorte com perspectiva corrigida. */
function recortarPapel(scanner: Jscanify, canvas: HTMLCanvasElement): HTMLCanvasElement | null {
  const cv = window.cv;
  const mat = cv.imread(canvas);
  let contorno: unknown = null;
  try {
    contorno = scanner.findPaperContour(mat);
    if (!contorno) return null;

    const cantos = scanner.getCornerPoints(contorno);
    // getCornerPoints pode deixar cantos indefinidos se o contorno for irregular.
    if (!cantos?.topLeftCorner || !cantos.topRightCorner || !cantos.bottomLeftCorner || !cantos.bottomRightCorner) {
      return null;
    }

    const { topLeftCorner: tl, topRightCorner: tr, bottomLeftCorner: bl, bottomRightCorner: br } = cantos;
    const largura = Math.max(distancia(tl, tr), distancia(bl, br));
    const altura = Math.max(distancia(tl, bl), distancia(tr, br));

    // Recorte pequeno demais = detecção ruim; melhor manter a foto inteira.
    if (largura < 96 || altura < 96) return null;
    const areaRecorte = largura * altura;
    const areaFoto = canvas.width * canvas.height;
    if (areaRecorte < areaFoto * 0.12) return null;

    return scanner.extractPaper(canvas, Math.round(largura), Math.round(altura), cantos);
  } catch {
    return null;
  } finally {
    (contorno as { delete?: () => void } | null)?.delete?.();
    mat.delete();
  }
}

function distancia(a: Ponto, b: Ponto): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

/**
 * Realce "documento": tons de cinza + normalização de iluminação (achata sombras e
 * clareia o papel) + máscara de nitidez (unsharp) para deixar as letras definidas.
 * Robusto para texto fotografado sob luz irregular.
 */
function realcarDocumento(canvas: HTMLCanvasElement): HTMLCanvasElement {
  const w = canvas.width;
  const h = canvas.height;
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  if (!ctx) return canvas;

  // 1. Tons de cinza.
  const src = ctx.getImageData(0, 0, w, h).data;
  const total = w * h;
  const cinza = new Float32Array(total);
  for (let i = 0, p = 0; p < total; i += 4, p++) {
    cinza[p] = 0.299 * src[i] + 0.587 * src[i + 1] + 0.114 * src[i + 2];
  }

  // Canvas auxiliar com o cinza, para os blurs via ctx.filter (suportado no iOS 14+).
  const cinzaCanvas = document.createElement('canvas');
  cinzaCanvas.width = w;
  cinzaCanvas.height = h;
  const cinzaCtx = cinzaCanvas.getContext('2d', { willReadFrequently: true });
  if (!cinzaCtx) return canvas;
  const cinzaImg = cinzaCtx.createImageData(w, h);
  for (let p = 0, o = 0; p < total; p++, o += 4) {
    cinzaImg.data[o] = cinzaImg.data[o + 1] = cinzaImg.data[o + 2] = cinza[p];
    cinzaImg.data[o + 3] = 255;
  }
  cinzaCtx.putImageData(cinzaImg, 0, 0);

  // 2. Fundo (iluminação): blur grande. 3. Detalhe: blur pequeno (para o unsharp).
  const fundo = lerBorrado(cinzaCanvas, Math.max(3, Math.round(Math.max(w, h) / 18)));
  const detalhe = lerBorrado(cinzaCanvas, Math.max(1, Math.round(Math.max(w, h) / 800)));

  // 4. Normaliza a iluminação, aplica unsharp e estica o contraste.
  const out = ctx.createImageData(w, h);
  const od = out.data;
  const nitidez = 0.8;
  const contraste = 1.35;
  for (let p = 0, o = 0; p < total; p++, o += 4) {
    const g = cinza[p];
    const bg = fundo[p] > 1 ? fundo[p] : 1;
    let v = (g / bg) * 235; // papel → quase branco; texto preservado
    v += nitidez * (g - detalhe[p]); // unsharp: realça as bordas das letras
    v = (v - 165) * contraste + 175; // contraste: afunda o texto, clareia o papel
    od[o] = od[o + 1] = od[o + 2] = clamp(v);
    od[o + 3] = 255;
  }
  ctx.putImageData(out, 0, 0);
  return canvas;
}

/** Luminância (canal R) de um canvas borrado com ctx.filter (blur de `raio` px). */
function lerBorrado(fonte: HTMLCanvasElement, raio: number): Float32Array {
  const w = fonte.width;
  const h = fonte.height;
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  const ctx = c.getContext('2d', { willReadFrequently: true });
  const out = new Float32Array(w * h);
  if (!ctx) return out;
  ctx.filter = `blur(${raio}px)`;
  ctx.drawImage(fonte, 0, 0);
  ctx.filter = 'none';
  const d = ctx.getImageData(0, 0, w, h).data;
  for (let p = 0, o = 0; p < out.length; p++, o += 4) out[p] = d[o];
  return out;
}

/** Realce "cor": mantém a cor, só dá um leve ganho de contraste/brilho. */
function realcarCor(canvas: HTMLCanvasElement): HTMLCanvasElement {
  const ctx = canvas.getContext('2d');
  if (!ctx) return canvas;
  const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
  const d = img.data;
  const contraste = 1.18;
  const brilho = 6;
  for (let i = 0; i < d.length; i += 4) {
    d[i] = clamp((d[i] - 128) * contraste + 128 + brilho);
    d[i + 1] = clamp((d[i + 1] - 128) * contraste + 128 + brilho);
    d[i + 2] = clamp((d[i + 2] - 128) * contraste + 128 + brilho);
  }
  ctx.putImageData(img, 0, 0);
  return canvas;
}

function clamp(v: number): number {
  return v < 0 ? 0 : v > 255 ? 255 : v;
}

/**
 * Desenha a fonte em um canvas novo, reduzindo para que o maior lado seja
 * `maxLado` px (evita PDFs gigantes e mantém o processamento rápido no celular).
 */
export function paraCanvas(
  fonte: HTMLImageElement | HTMLCanvasElement,
  maxLado: number,
): HTMLCanvasElement {
  const larguraOrig = fonte instanceof HTMLImageElement ? fonte.naturalWidth : fonte.width;
  const alturaOrig = fonte instanceof HTMLImageElement ? fonte.naturalHeight : fonte.height;
  const escala = Math.min(1, maxLado / Math.max(larguraOrig, alturaOrig));
  const largura = Math.max(1, Math.round(larguraOrig * escala));
  const altura = Math.max(1, Math.round(alturaOrig * escala));

  const canvas = document.createElement('canvas');
  canvas.width = largura;
  canvas.height = altura;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(fonte, 0, 0, largura, altura);
  }
  return canvas;
}

/** Carrega um File/dataURL em um HTMLImageElement (fallback do <input type=file>). */
export function carregarImagem(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Falha ao carregar a imagem.'));
    img.src = src;
  });
}

/** Lê um File como dataURL. */
export function arquivoParaDataUrl(arquivo: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error('Falha ao ler o arquivo.'));
    reader.readAsDataURL(arquivo);
  });
}
