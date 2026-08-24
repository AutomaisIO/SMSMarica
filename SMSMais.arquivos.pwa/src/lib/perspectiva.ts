/**
 * Correção de perspectiva ("desentortar" a folha) 100% no cliente.
 *
 * Dado o quadrilátero da página (4 cantos marcados na foto), produz um retângulo
 * recortado e planificado **na proporção A4** (a folha típica de exame), preenchendo
 * 100% da largura e da altura — sem áreas vazias. A orientação (retrato x paisagem)
 * é decidida pelo próprio quadrilátero. Saída em alta resolução (~210 dpi) para
 * preservar a legibilidade do texto. O warp usa OpenCV quando disponível e tem
 * fallback puro em JS (homografia + amostragem bilinear).
 */

export type Ponto = { x: number; y: number };
export type Cantos = { tl: Ponto; tr: Ponto; br: Ponto; bl: Ponto };

/** Lado maior da saída em px (A4 a ~210 dpi → boa nitidez de texto sem PDF gigante). */
const LADO_A4 = 2480;
/** Proporção A4 (297/210). */
const RATIO_A4 = Math.SQRT2;

/** Ordena 4 pontos quaisquer em topo-esq, topo-dir, baixo-dir, baixo-esq. */
export function ordenarCantos(pts: Ponto[]): Cantos {
  // tl = menor (x+y); br = maior (x+y); tr = maior (x−y); bl = menor (x−y).
  let tl = pts[0], br = pts[0], tr = pts[0], bl = pts[0];
  for (const p of pts) {
    if (p.x + p.y < tl.x + tl.y) tl = p;
    if (p.x + p.y > br.x + br.y) br = p;
    if (p.x - p.y > tr.x - tr.y) tr = p;
    if (p.x - p.y < bl.x - bl.y) bl = p;
  }
  return { tl, tr, br, bl };
}

function dist(a: Ponto, b: Ponto): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

/**
 * Dimensões de saída em A4: o lado maior fica com LADO_A4 e o menor segue a razão A4.
 * Retrato se o quadrilátero é mais alto que largo; paisagem caso contrário.
 */
function dimensoesA4(c: Cantos): { largura: number; altura: number } {
  const larg = (dist(c.tl, c.tr) + dist(c.bl, c.br)) / 2;
  const alt = (dist(c.tl, c.bl) + dist(c.tr, c.br)) / 2;
  const menor = Math.round(LADO_A4 / RATIO_A4);
  return larg > alt
    ? { largura: LADO_A4, altura: menor } // paisagem
    : { largura: menor, altura: LADO_A4 }; // retrato
}

/**
 * Recorta + planifica a folha: warp do quadrilátero `cantos` (em coords da imagem
 * `fonte`) para um retângulo A4 cheio. Devolve um canvas novo.
 */
export function corrigirPerspectiva(fonte: HTMLCanvasElement, cantos: Cantos): HTMLCanvasElement {
  const { largura, altura } = dimensoesA4(cantos);

  // Caminho preferido: OpenCV (rápido, alta qualidade) quando carregado.
  if (window.cv?.Mat) {
    try {
      return warpOpenCv(fonte, cantos, largura, altura);
    } catch {
      /* cai para o warp em JS */
    }
  }
  return warpJs(fonte, cantos, largura, altura);
}

/** Warp via OpenCV (getPerspectiveTransform + warpPerspective). */
function warpOpenCv(
  fonte: HTMLCanvasElement,
  c: Cantos,
  largura: number,
  altura: number,
): HTMLCanvasElement {
  const cv = window.cv;
  const src = cv.imread(fonte);
  const dst = new cv.Mat();
  const de = cv.matFromArray(4, 1, cv.CV_32FC2, [
    c.tl.x, c.tl.y, c.tr.x, c.tr.y, c.br.x, c.br.y, c.bl.x, c.bl.y,
  ]);
  const para = cv.matFromArray(4, 1, cv.CV_32FC2, [
    0, 0, largura, 0, largura, altura, 0, altura,
  ]);
  const m = cv.getPerspectiveTransform(de, para);
  try {
    cv.warpPerspective(
      src,
      dst,
      m,
      new cv.Size(largura, altura),
      cv.INTER_LINEAR,
      cv.BORDER_REPLICATE,
      new cv.Scalar(),
    );
    const out = document.createElement('canvas');
    out.width = largura;
    out.height = altura;
    cv.imshow(out, dst);
    return out;
  } finally {
    src.delete();
    dst.delete();
    de.delete();
    para.delete();
    m.delete();
  }
}

/**
 * Warp puro em JS (fallback sem OpenCV): mapeamento inverso (saída → fonte) via
 * homografia + amostragem bilinear. Suficiente para uma página por vez.
 */
function warpJs(
  fonte: HTMLCanvasElement,
  c: Cantos,
  largura: number,
  altura: number,
): HTMLCanvasElement {
  const sctx = fonte.getContext('2d', { willReadFrequently: true });
  const out = document.createElement('canvas');
  out.width = largura;
  out.height = altura;
  const octx = out.getContext('2d');
  if (!sctx || !octx) return out;

  const sw = fonte.width;
  const sh = fonte.height;
  const sdata = sctx.getImageData(0, 0, sw, sh).data;
  const odata = octx.createImageData(largura, altura);
  const od = odata.data;

  // Homografia que leva o retângulo de saída → quadrilátero fonte.
  const h = resolverHomografia(
    [
      { x: 0, y: 0 },
      { x: largura, y: 0 },
      { x: largura, y: altura },
      { x: 0, y: altura },
    ],
    [c.tl, c.tr, c.br, c.bl],
  );
  if (!h) {
    octx.drawImage(fonte, 0, 0, largura, altura);
    return out;
  }
  const [a, b, cc, d, e, f, g, k] = h;

  for (let y = 0; y < altura; y++) {
    for (let x = 0; x < largura; x++) {
      const w = g * x + k * y + 1;
      const su = (a * x + b * y + cc) / w;
      const sv = (d * x + e * y + f) / w;
      const oi = (y * largura + x) * 4;

      if (su < 0 || sv < 0 || su > sw - 1 || sv > sh - 1) {
        od[oi] = od[oi + 1] = od[oi + 2] = 255;
        od[oi + 3] = 255;
        continue;
      }
      const x0 = Math.floor(su);
      const y0 = Math.floor(sv);
      const x1 = x0 + 1 < sw ? x0 + 1 : x0;
      const y1 = y0 + 1 < sh ? y0 + 1 : y0;
      const fx = su - x0;
      const fy = sv - y0;
      const i00 = (y0 * sw + x0) * 4;
      const i10 = (y0 * sw + x1) * 4;
      const i01 = (y1 * sw + x0) * 4;
      const i11 = (y1 * sw + x1) * 4;
      for (let ch = 0; ch < 3; ch++) {
        const top = sdata[i00 + ch] + (sdata[i10 + ch] - sdata[i00 + ch]) * fx;
        const bot = sdata[i01 + ch] + (sdata[i11 + ch] - sdata[i01 + ch]) * fx;
        od[oi + ch] = top + (bot - top) * fy;
      }
      od[oi + 3] = 255;
    }
  }
  octx.putImageData(odata, 0, 0);
  return out;
}

/**
 * Resolve a homografia 3×3 (h33=1) que mapeia `de[i]` → `para[i]` (4 correspondências),
 * via eliminação de Gauss num sistema 8×8. Retorna [h0..h7] ou null se singular.
 */
function resolverHomografia(de: Ponto[], para: Ponto[]): number[] | null {
  const A: number[][] = [];
  const b: number[] = [];
  for (let i = 0; i < 4; i++) {
    const X = de[i].x;
    const Y = de[i].y;
    const x = para[i].x;
    const y = para[i].y;
    A.push([X, Y, 1, 0, 0, 0, -X * x, -Y * x]);
    b.push(x);
    A.push([0, 0, 0, X, Y, 1, -X * y, -Y * y]);
    b.push(y);
  }
  return resolverGauss(A, b);
}

/** Eliminação de Gauss com pivotamento parcial (n×n). */
function resolverGauss(A: number[][], b: number[]): number[] | null {
  const n = b.length;
  const m = A.map((linha, i) => [...linha, b[i]]);
  for (let col = 0; col < n; col++) {
    let piv = col;
    for (let r = col + 1; r < n; r++) {
      if (Math.abs(m[r][col]) > Math.abs(m[piv][col])) piv = r;
    }
    if (Math.abs(m[piv][col]) < 1e-12) return null;
    [m[col], m[piv]] = [m[piv], m[col]];
    const d = m[col][col];
    for (let j = col; j <= n; j++) m[col][j] /= d;
    for (let r = 0; r < n; r++) {
      if (r === col) continue;
      const fator = m[r][col];
      if (fator === 0) continue;
      for (let j = col; j <= n; j++) m[r][j] -= fator * m[col][j];
    }
  }
  return m.map((linha) => linha[n]);
}
