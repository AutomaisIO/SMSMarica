// Planta do veículo (vista de cima, frente à direita, teto "tirado") com os bancos no lugar.
//
// Cada modelo tem as medidas reais — comprimento, largura, onde termina o capô, onde começa o
// teto (topo do para-brisa), onde o teto acaba — deduzidas dos mesmos perfis laterais usados no
// desenho de lado (desenhoVeiculo.ts). As fileiras caem onde os bancos ficam no carro de verdade:
// Onix e Polo com dois bancos na frente e o banco inteiriço atrás; Spin com a terceira fileira
// sobre o eixo traseiro; Sprinter e Master com motorista + banco duplo na cabine e as fileiras de
// passageiros no passo de van (≈ 0,8 m), corredor do lado da porta corrediça.
//
// O layout cadastrado manda: fileiras e assentos são os do cadastro. A planta só decide ONDE cada
// um fica. Fileira a mais do que o carro comporta aperta o passo em vez de sumir.
//
// Coordenadas em centímetros. x: 0 = traseira → L = frente. y: 0 = lado esquerdo (motorista) → W.

import type { TipoAssento, TipoVeiculo } from '@/features/veiculos/types';
import { escolherDesenho, type ChaveDesenho } from '@/features/veiculos/lib/desenhoVeiculo';
import {
  CORES_FANTASIA,
  clarear,
  contornoDaCor,
  escurecer,
  resolverCorVeiculo,
} from '@/features/veiculos/lib/corVeiculo';

type Classe = 'carro' | 'van' | 'onibus';

type Formato = {
  /** Largura da ponta em fração de W. */
  largura: number;
  /** Raio do canto (m). */
  raio: number;
  /** Quanto o centro da ponta avança além dos cantos (m). */
  bojo: number;
};

type Planta = {
  classe: Classe;
  /** Comprimento e largura da carroceria (m). */
  L: number;
  W: number;
  /** Espessura da lateral (porta + moldura do teto) vista de cima (m). */
  lateral: number;
  /** Medidas a partir da FRENTE (m): fim do capô, topo do para-brisa, fim do teto. */
  capo: number;
  paraBrisa: number;
  fimTeto: number;
  /** Vidro traseiro, da frente (m) — só carros. */
  vidroTraseiro?: [number, number];
  /** Caçamba da picape, da frente (m). */
  cacamba?: [number, number];
  /** Vão da porta do lado direito (corrediça da van, porta do ônibus), da frente (m). */
  porta?: [number, number];
  /** Centro das fileiras, da frente (m). */
  fileiras: { primeira: number; segunda: number; passo: number; ultimaMax: number };
  nariz: Formato;
  traseira: Formato;
};

const PLANTA_ONIX: Planta = {
  classe: 'carro',
  L: 4.163,
  W: 1.73,
  lateral: 0.16,
  capo: 1.09,
  paraBrisa: 1.67,
  fimTeto: 3.42,
  vidroTraseiro: [3.42, 3.94],
  fileiras: { primeira: 2.02, segunda: 2.93, passo: 0.8, ultimaMax: 3.8 },
  nariz: { largura: 0.84, raio: 0.3, bojo: 0.16 },
  traseira: { largura: 0.9, raio: 0.24, bojo: 0.08 },
};

const PLANTA_SPIN: Planta = {
  classe: 'carro',
  L: 4.363,
  W: 1.735,
  lateral: 0.15,
  capo: 1.12,
  paraBrisa: 1.74,
  fimTeto: 3.95,
  vidroTraseiro: [3.95, 4.2],
  fileiras: { primeira: 2.05, segunda: 2.93, passo: 0.78, ultimaMax: 4.0 },
  nariz: { largura: 0.86, raio: 0.32, bojo: 0.17 },
  traseira: { largura: 0.94, raio: 0.22, bojo: 0.05 },
};

const PLANTA_MASTER: Planta = {
  classe: 'van',
  L: 6.198,
  W: 2.07,
  lateral: 0.11,
  capo: 1.11,
  paraBrisa: 1.8,
  fimTeto: 6.12,
  porta: [2.45, 3.62],
  fileiras: { primeira: 2.3, segunda: 3.2, passo: 0.78, ultimaMax: 5.85 },
  nariz: { largura: 0.88, raio: 0.36, bojo: 0.2 },
  traseira: { largura: 0.99, raio: 0.08, bojo: 0.02 },
};

const PLANTAS: Record<ChaveDesenho, Planta> = {
  onix: PLANTA_ONIX,
  hatch: PLANTA_ONIX,
  polo: {
    classe: 'carro',
    L: 4.057,
    W: 1.751,
    lateral: 0.16,
    capo: 1.02,
    paraBrisa: 1.76,
    fimTeto: 3.58,
    vidroTraseiro: [3.58, 3.93],
    fileiras: { primeira: 2.0, segunda: 2.9, passo: 0.8, ultimaMax: 3.7 },
    nariz: { largura: 0.86, raio: 0.3, bojo: 0.14 },
    traseira: { largura: 0.92, raio: 0.26, bojo: 0.07 },
  },
  sedan: {
    ...PLANTA_ONIX,
    L: 4.474,
    fimTeto: 3.15,
    vidroTraseiro: [3.15, 3.62],
    fileiras: { primeira: 2.02, segunda: 2.92, passo: 0.8, ultimaMax: 3.9 },
  },
  spin: PLANTA_SPIN,

  minivan: PLANTA_SPIN,
  suv: {
    classe: 'carro',
    L: 4.3,
    W: 1.8,
    lateral: 0.17,
    capo: 1.1,
    paraBrisa: 1.75,
    fimTeto: 3.85,
    vidroTraseiro: [3.85, 4.12],
    fileiras: { primeira: 2.05, segunda: 2.95, passo: 0.78, ultimaMax: 3.95 },
    nariz: { largura: 0.86, raio: 0.3, bojo: 0.15 },
    traseira: { largura: 0.92, raio: 0.22, bojo: 0.06 },
  },
  picape: {
    classe: 'carro',
    L: 4.47,
    W: 1.73,
    lateral: 0.16,
    capo: 1.12,
    paraBrisa: 1.75,
    fimTeto: 3.12,
    cacamba: [3.24, 4.36],
    fileiras: { primeira: 2.05, segunda: 2.85, passo: 0.75, ultimaMax: 3.1 },
    nariz: { largura: 0.86, raio: 0.3, bojo: 0.15 },
    traseira: { largura: 0.96, raio: 0.1, bojo: 0.03 },
  },
  sprinter: {
    classe: 'van',
    L: 7.0,
    W: 2.0,
    lateral: 0.1,
    capo: 1.03,
    paraBrisa: 1.83,
    fimTeto: 6.92,
    porta: [2.58, 3.82],
    fileiras: { primeira: 2.3, segunda: 3.3, passo: 0.8, ultimaMax: 6.6 },
    nariz: { largura: 0.8, raio: 0.36, bojo: 0.24 },
    traseira: { largura: 0.99, raio: 0.08, bojo: 0.02 },
  },
  master: PLANTA_MASTER,

  van: PLANTA_MASTER,
  microOnibus: {
    classe: 'onibus',
    L: 8.0,
    W: 2.3,
    lateral: 0.1,
    capo: 0.08,
    paraBrisa: 0.7,
    fimTeto: 7.88,
    porta: [0.75, 1.75],
    fileiras: { primeira: 1.35, segunda: 2.4, passo: 0.76, ultimaMax: 7.55 },
    nariz: { largura: 0.95, raio: 0.24, bojo: 0.1 },
    traseira: { largura: 0.97, raio: 0.12, bojo: 0.04 },
  },
  onibus: {
    classe: 'onibus',
    L: 12.0,
    W: 2.5,
    lateral: 0.1,
    capo: 0.06,
    paraBrisa: 0.55,
    fimTeto: 11.88,
    porta: [0.6, 1.8],
    fileiras: { primeira: 1.4, segunda: 2.6, passo: 0.78, ultimaMax: 11.5 },
    nariz: { largura: 0.97, raio: 0.18, bojo: 0.06 },
    traseira: { largura: 0.98, raio: 0.12, bojo: 0.03 },
  },
};

// ── Entrada e saída ─────────────────────────────────────────────────────────

export type AssentoEntrada = { numero: number; tipo: TipoAssento; bloqueado?: boolean };
export type FileiraEntrada = { ordem: number; assentos: AssentoEntrada[] };

export type VeiculoPlanta = {
  tipo: TipoVeiculo;
  modelo?: string | null;
  fabricante?: string | null;
  cor: string;
};

export type AssentoNaPlanta = AssentoEntrada & {
  fileiraOrdem: number;
  /** Centro e tamanho, em cm, no sistema do viewBox. */
  cx: number;
  cy: number;
  largura: number;
  profundidade: number;
};

export type PlantaMontada = {
  svg: string;
  /** viewBox em cm: [x, y, largura, altura]. */
  caixa: [number, number, number, number];
  assentos: AssentoNaPlanta[];
  /** Centro (x, em cm) de cada fileira — para os rótulos F1, F2… */
  fileiras: Array<{ ordem: number; cx: number }>;
  /** Comprimento real (m) — base para o tamanho mínimo na tela. */
  comprimento: number;
};

// ── Posição dos bancos ──────────────────────────────────────────────────────

/** Centros (m, a partir da parede esquerda interna) e largura dos bancos de uma fileira. */
function transversal(classe: Classe, primeira: boolean, k: number, Win: number): { centros: number[]; s: number } {
  const espalhar = (s: number) => {
    const passo = Math.min(s + 0.04, Win / k);
    return {
      centros: Array.from({ length: k }, (_, i) => Win / 2 + (i - (k - 1) / 2) * passo),
      s: Math.min(s, passo - 0.03),
    };
  };

  if (classe === 'carro') {
    if (k === 2) {
      const s = primeira ? 0.52 : 0.46;
      const d = Math.min(0.36, Win / 2 - s / 2 - 0.03);
      return { centros: [Win / 2 - d, Win / 2 + d], s };
    }
    return espalhar(primeira && k === 1 ? 0.52 : 0.45);
  }

  if (classe === 'van') {
    if (primeira) {
      if (k === 1) return { centros: [0.45], s: 0.5 };
      return espalhar(0.5);
    }
    const s = Math.min(0.44, (Win - 0.4) / 3);
    const esq1 = s / 2 + 0.02;
    const esq2 = esq1 + s + 0.02;
    const dir = Win - s / 2 - 0.02;
    if (k === 1) return { centros: [dir], s };
    if (k === 2) return { centros: [esq1, esq2], s };
    if (k === 3) return { centros: [esq1, esq2, dir], s };
    return espalhar(0.44);
  }

  // Ônibus: motorista na esquerda, corredor no meio (2 + 2).
  if (primeira) {
    if (k === 1) return { centros: [0.45], s: 0.5 };
    if (k === 2) return { centros: [0.45, Win - 0.45], s: 0.5 };
    return espalhar(0.5);
  }
  const s = Math.min(0.45, (Win - 0.45) / 4);
  const e1 = s / 2 + 0.03;
  const e2 = e1 + s + 0.02;
  const d2 = Win - s / 2 - 0.03;
  const d1 = d2 - s - 0.02;
  if (k === 1) return { centros: [e1], s };
  if (k === 2) return { centros: [e1, e2], s };
  if (k === 3) return { centros: [e1, e2, d2], s };
  if (k === 4) return { centros: [e1, e2, d1, d2], s };
  return espalhar(0.45);
}

/** Centro de cada fileira, da frente (m). Fileira a mais aperta o passo. */
function longitudinal(p: Planta, n: number): number[] {
  const { primeira, segunda, passo, ultimaMax } = p.fileiras;
  if (n <= 0) return [];
  const ds = [primeira];
  if (n === 1) return ds;
  let seg = Math.min(segunda, ultimaMax);
  let pas = passo;
  if (n > 2 && seg + (n - 2) * pas > ultimaMax) pas = (ultimaMax - seg) / (n - 2);
  if (n > 2 && pas < 0.2) {
    // Layout muito maior que o veículo: distribui tudo entre a primeira e o fundo.
    seg = primeira + (ultimaMax - primeira) / (n - 1);
    pas = (ultimaMax - seg) / Math.max(1, n - 2);
  }
  for (let i = 0; i < n - 1; i++) ds.push(seg + i * pas);
  return ds;
}

// ── Contorno ────────────────────────────────────────────────────────────────

const n2 = (v: number) => Math.round(v * 10) / 10;

function contorno(L: number, W: number, nz: Formato, tr: Formato): string {
  const yfT = ((1 - nz.largura) * W) / 2;
  const yfB = W - yfT;
  const yrT = ((1 - tr.largura) * W) / 2;
  const yrB = W - yrT;
  const [bf, rf, br, rr] = [nz.bojo, nz.raio, tr.bojo, tr.raio];
  const c = [
    `M${n2(br + rr)} ${n2(yrT)}`,
    `C${n2(L * 0.35)} ${n2(-W * 0.012)} ${n2(L * 0.62)} ${n2(-W * 0.012)} ${n2(L - bf - rf)} ${n2(yfT)}`,
    `Q${n2(L - bf * 0.55)} ${n2(yfT)} ${n2(L - bf * 0.35)} ${n2(yfT + rf)}`,
    `C${n2(L + bf * 0.12)} ${n2(W * 0.36)} ${n2(L + bf * 0.12)} ${n2(W * 0.64)} ${n2(L - bf * 0.35)} ${n2(yfB - rf)}`,
    `Q${n2(L - bf * 0.55)} ${n2(yfB)} ${n2(L - bf - rf)} ${n2(yfB)}`,
    `C${n2(L * 0.62)} ${n2(W * 1.012)} ${n2(L * 0.35)} ${n2(W * 1.012)} ${n2(br + rr)} ${n2(yrB)}`,
    `Q${n2(br * 0.5)} ${n2(yrB)} ${n2(br * 0.3)} ${n2(yrB - rr)}`,
    `C${n2(-br * 0.12)} ${n2(W * 0.64)} ${n2(-br * 0.12)} ${n2(W * 0.36)} ${n2(br * 0.3)} ${n2(yrT + rr)}`,
    `Q${n2(br * 0.5)} ${n2(yrT)} ${n2(br + rr)} ${n2(yrT)}Z`,
  ];
  return c.join('');
}

function hash(texto: string): string {
  let h = 5381;
  for (let i = 0; i < texto.length; i++) h = ((h << 5) + h + texto.charCodeAt(i)) >>> 0;
  return h.toString(36);
}

// ── Montagem ────────────────────────────────────────────────────────────────

export function montarPlanta(veiculo: VeiculoPlanta, linhas: FileiraEntrada[]): PlantaMontada {
  const { chave } = escolherDesenho(veiculo.tipo, veiculo.modelo, veiculo.fabricante);
  const p = PLANTAS[chave];
  const cor = resolverCorVeiculo(veiculo.cor);
  const hex = cor.hex;
  const contornoCor = contornoDaCor(hex);
  const id = `vp${hash(`${chave}|${hex}|${cor.fantasia}`)}`;

  // Tudo em cm a partir daqui.
  const L = p.L * 100;
  const W = p.W * 100;
  const lat = p.lateral * 100;
  const Win = p.W - 2 * p.lateral;
  const xDaFrente = (d: number) => L - d * 100;

  // Bancos.
  const ordenadas = [...linhas].sort((a, b) => a.ordem - b.ordem);
  const ds = longitudinal(p, ordenadas.length);
  const passoMin = ds.length > 1 ? Math.min(...ds.slice(1).map((d, i) => d - ds[i])) : 1;
  const profundidadeIdeal = p.classe === 'carro' ? 0.5 : 0.46;
  const profundidade = Math.max(0.16, Math.min(profundidadeIdeal, passoMin - 0.1));

  const assentos: AssentoNaPlanta[] = [];
  const fileiras: PlantaMontada['fileiras'] = [];
  ordenadas.forEach((f, i) => {
    const doLado = [...f.assentos].sort((a, b) => a.numero - b.numero);
    const { centros, s } = transversal(p.classe, i === 0, doLado.length, Win);
    const cx = xDaFrente(ds[i]);
    fileiras.push({ ordem: f.ordem, cx });
    doLado.forEach((a, j) => {
      assentos.push({
        ...a,
        fileiraOrdem: f.ordem,
        cx,
        cy: lat + centros[j] * 100,
        largura: Math.max(14, s * 100),
        profundidade: profundidade * 100,
      });
    });
  });

  // O interior vai do capô até o fim do teto — ou até a última fileira, se ela passar dele.
  const ultimaD = ds.length ? ds[ds.length - 1] + profundidade / 2 + 0.1 : p.fimTeto;
  const fimInterior = Math.min(p.L - 0.1, Math.max(p.fimTeto, ultimaD));
  const xIntTras = xDaFrente(fimInterior);
  const xIntFrente = xDaFrente(p.capo + 0.04);
  const xCapo = xDaFrente(p.capo);
  const xParaBrisa = xDaFrente(p.paraBrisa);

  const corpo = contorno(L, W, p.nariz, p.traseira);
  const partes: string[] = [];

  const pintura = cor.fantasia
    ? `<linearGradient id="${id}c" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="${L}" y2="0">${CORES_FANTASIA.map(
        (c, i) => `<stop offset="${i / (CORES_FANTASIA.length - 1)}" stop-color="${c}"/>`,
      ).join('')}</linearGradient>`
    : `<linearGradient id="${id}c" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="0" y2="${W}">` +
      `<stop offset="0" stop-color="${escurecer(hex, 0.2)}"/>` +
      `<stop offset=".22" stop-color="${hex}"/>` +
      `<stop offset=".5" stop-color="${clarear(hex, 0.22)}"/>` +
      `<stop offset=".78" stop-color="${hex}"/>` +
      `<stop offset="1" stop-color="${escurecer(hex, 0.2)}"/></linearGradient>`;

  partes.push(
    `<defs>${pintura}` +
      `<linearGradient id="${id}v" x1="0" y1="0" x2="1" y2="1">` +
      '<stop offset="0" stop-color="#7d8c9c"/><stop offset=".45" stop-color="#34414f"/>' +
      '<stop offset=".46" stop-color="#27313c"/><stop offset="1" stop-color="#141b23"/></linearGradient>' +
      `<linearGradient id="${id}p" x1="0" y1="0" x2="0" y2="1">` +
      '<stop offset="0" stop-color="#e3e6ea"/><stop offset=".5" stop-color="#eef0f2"/><stop offset="1" stop-color="#e3e6ea"/>' +
      '</linearGradient></defs>',
  );

  // Sombra.
  partes.push(
    `<path d="${corpo}" transform="translate(4 6)" fill="#000" opacity=".12"/>`,
  );

  // Retrovisores (antes da carroceria, para a base ficar por baixo).
  const dRetro = p.classe === 'onibus' ? 0.25 : p.capo + (p.classe === 'van' ? 0.22 : 0.16);
  const xr = xDaFrente(dRetro);
  const saliencia = p.classe === 'carro' ? 14 : 20;
  const compRetro = p.classe === 'carro' ? 16 : 18;
  const corRetro = p.classe === 'carro' ? `url(#${id}c)` : '#1f2328';
  partes.push(
    `<path d="M${xr} 4L${xr - 3} ${-saliencia + 2}Q${xr - compRetro / 2} ${-saliencia - 2} ${xr - compRetro} ${-saliencia + 3}L${xr - compRetro - 4} 6Z" fill="${corRetro}" stroke="${contornoCor}" stroke-width="1.2"/>` +
      `<path d="M${xr} ${W - 4}L${xr - 3} ${W + saliencia - 2}Q${xr - compRetro / 2} ${W + saliencia + 2} ${xr - compRetro} ${W + saliencia - 3}L${xr - compRetro - 4} ${W - 6}Z" fill="${corRetro}" stroke="${contornoCor}" stroke-width="1.2"/>`,
  );

  // Carroceria.
  partes.push(`<path d="${corpo}" fill="url(#${id}c)" stroke="${contornoCor}" stroke-width="1.6" stroke-linejoin="round"/>`);

  // Vincos do capô (carros).
  if (p.classe === 'carro') {
    const yT = W * 0.3;
    const yB = W * 0.7;
    partes.push(
      `<path d="M${xCapo - 6} ${yT}Q${L - 40} ${W * 0.33} ${L - 18} ${W * 0.36}M${xCapo - 6} ${yB}Q${L - 40} ${W * 0.67} ${L - 18} ${W * 0.64}" fill="none" stroke="#fff" stroke-opacity=".35" stroke-width="1.4"/>`,
    );
  }

  // Faróis e lanternas.
  const yfT = ((1 - p.nariz.largura) * W) / 2;
  const yrT = ((1 - p.traseira.largura) * W) / 2;
  const fx = L - p.nariz.bojo * 100 * 0.5 - (p.classe === 'onibus' ? 6 : 9);
  const fy = yfT + p.nariz.raio * 100 * 0.45;
  const [rxF, ryF] = p.classe === 'onibus' ? [4, 14] : [6, 17];
  partes.push(
    `<ellipse cx="${n2(fx)}" cy="${n2(fy + 4)}" rx="${rxF}" ry="${ryF}" transform="rotate(-18 ${n2(fx)} ${n2(fy + 4)})" fill="#fdf6dc" stroke="#8a9199" stroke-width="1"/>` +
      `<ellipse cx="${n2(fx)}" cy="${n2(W - fy - 4)}" rx="${rxF}" ry="${ryF}" transform="rotate(18 ${n2(fx)} ${n2(W - fy - 4)})" fill="#fdf6dc" stroke="#8a9199" stroke-width="1"/>`,
  );
  const tx = p.traseira.bojo * 100 * 0.4 + 3;
  const ty = yrT + Math.max(6, p.traseira.raio * 100 * 0.35);
  partes.push(
    `<rect x="${n2(tx)}" y="${n2(ty)}" width="7" height="${p.classe === 'carro' ? 26 : 22}" rx="3" fill="#c81e1e" stroke="#7f1d1d" stroke-width="1"/>` +
      `<rect x="${n2(tx)}" y="${n2(W - ty - (p.classe === 'carro' ? 26 : 22))}" width="7" height="${p.classe === 'carro' ? 26 : 22}" rx="3" fill="#c81e1e" stroke="#7f1d1d" stroke-width="1"/>`,
  );

  // Vidro traseiro (carros) e caçamba (picape).
  if (p.vidroTraseiro && fimInterior < p.vidroTraseiro[1] - 0.1) {
    const x1 = xDaFrente(p.vidroTraseiro[1]);
    const x2 = xDaFrente(Math.max(fimInterior, p.vidroTraseiro[0]));
    const m1 = W * 0.2;
    const m2 = W * 0.14;
    partes.push(
      `<path d="M${n2(x2)} ${n2(m2)}L${n2(x1)} ${n2(m1)}Q${n2(x1 - 4)} ${W / 2} ${n2(x1)} ${n2(W - m1)}L${n2(x2)} ${n2(W - m2)}Z" fill="url(#${id}v)" stroke="#0e1318" stroke-width="1"/>`,
    );
  }
  if (p.cacamba) {
    const x1 = xDaFrente(p.cacamba[1]);
    const x2 = xDaFrente(p.cacamba[0]);
    const m = lat * 0.7;
    const fundo = escurecer(hex, 0.3);
    partes.push(
      `<rect x="${n2(x1)}" y="${n2(m)}" width="${n2(x2 - x1)}" height="${n2(W - 2 * m)}" rx="5" fill="${fundo}" stroke="${contornoCor}" stroke-width="1.2"/>` +
        [0.25, 0.5, 0.75]
          .map((f) => `<path d="M${n2(x1 + 6)} ${n2(m + (W - 2 * m) * f)}H${n2(x2 - 6)}" stroke="${escurecer(hex, 0.45)}" stroke-width="2"/>`)
          .join(''),
    );
  }

  // Interior (o teto "tirado").
  const r = p.classe === 'carro' ? 16 : 10;
  partes.push(
    `<rect x="${n2(xIntTras)}" y="${n2(lat)}" width="${n2(xIntFrente - xIntTras)}" height="${n2(W - 2 * lat)}" rx="${r}" fill="url(#${id}p)" stroke="${escurecer(hex, 0.35)}" stroke-width="1.6"/>`,
  );

  // Porta do lado direito: o degrau aparece na parede.
  if (p.porta) {
    const x1 = xDaFrente(p.porta[1]);
    const x2 = xDaFrente(p.porta[0]);
    partes.push(
      `<rect x="${n2(x1)}" y="${n2(W - lat - 1)}" width="${n2(x2 - x1)}" height="${n2(lat + 1)}" fill="#cfd5dc" stroke="${escurecer(hex, 0.35)}" stroke-width="1"/>` +
        `<path d="M${n2(x1 + 4)} ${n2(W - lat / 2)}H${n2(x2 - 4)}" stroke="#9aa3ad" stroke-width="1.2" stroke-dasharray="4 3"/>`,
    );
  }

  // Painel, console e volante.
  const fundoPainel = p.classe === 'carro' ? 0.4 : 0.45;
  const xPainelTras = xDaFrente(p.capo + fundoPainel);
  partes.push(
    `<path d="M${n2(xPainelTras)} ${n2(lat + 2)}H${n2(xIntFrente - 2)}V${n2(W - lat - 2)}H${n2(xPainelTras)}Q${n2(xPainelTras - 5)} ${W / 2} ${n2(xPainelTras)} ${n2(lat + 2)}Z" fill="#555b63"/>`,
  );
  const motorista =
    assentos.find((a) => a.fileiraOrdem === ordenadas[0]?.ordem && a.tipo === 'Motorista') ??
    assentos.find((a) => a.fileiraOrdem === ordenadas[0]?.ordem);
  if (motorista) {
    if (p.classe === 'carro' && ordenadas[0].assentos.length === 2) {
      partes.push(
        `<rect x="${n2(motorista.cx - 20)}" y="${n2(W / 2 - 9)}" width="${n2(xPainelTras - motorista.cx + 20)}" height="18" rx="6" fill="#c9ced4" stroke="#aab1b9" stroke-width="1"/>`,
      );
    }
    const xv = Math.min(xPainelTras - 3, motorista.cx + motorista.profundidade / 2 + 16);
    partes.push(
      `<rect x="${n2(xv - 3)}" y="${n2(motorista.cy - 17)}" width="7" height="34" rx="3.5" fill="none" stroke="#1f2328" stroke-width="3.2"/>` +
        `<rect x="${n2(xv - 2)}" y="${n2(motorista.cy - 4)}" width="${n2(xPainelTras - xv + 2)}" height="8" rx="3" fill="#2a2f35"/>`,
    );
  }

  // Para-brisa (translúcido, o painel aparece por baixo).
  const mb = p.classe === 'carro' ? W * 0.08 : W * 0.05;
  const mt = p.classe === 'carro' ? W * 0.13 : W * 0.06;
  partes.push(
    `<path d="M${n2(xParaBrisa)} ${n2(mt)}L${n2(xCapo)} ${n2(mb)}Q${n2(xCapo + 10)} ${W / 2} ${n2(xCapo)} ${n2(W - mb)}L${n2(xParaBrisa)} ${n2(W - mt)}Z" fill="url(#${id}v)" opacity=".32" stroke="#0e1318" stroke-width="1"/>` +
      `<path d="M${n2(xParaBrisa + (xCapo - xParaBrisa) * 0.3)} ${n2(W * 0.22)}L${n2(xParaBrisa + (xCapo - xParaBrisa) * 0.7)} ${n2(W * 0.4)}" stroke="#fff" stroke-opacity=".3" stroke-width="3" stroke-linecap="round"/>`,
  );

  // Ambulância: cruz no capô.
  if (veiculo.tipo === 'Ambulancia') {
    const cx = (xCapo + L) / 2 - 4;
    const cy = W / 2;
    const b = 7;
    const l = 17;
    partes.push(
      `<circle cx="${n2(cx)}" cy="${cy}" r="${l + 5}" fill="#fff" stroke="#d32f2f" stroke-width="1.5"/>` +
        `<path d="M${cx - b} ${cy - l}H${cx + b}V${cy - b}H${cx + l}V${cy + b}H${cx + b}V${cy + l}H${cx - b}V${cy + b}H${cx - l}V${cy - b}H${cx - b}Z" fill="#dc2626"/>`,
    );
  }

  const mx = 10;
  const my = 26;
  const caixa: [number, number, number, number] = [-mx, -my, L + 2 * mx, W + 2 * my];
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${caixa.join(' ')}" preserveAspectRatio="xMidYMid meet">${partes.join('')}</svg>`;

  return { svg, caixa, assentos, fileiras, comprimento: p.L };
}
