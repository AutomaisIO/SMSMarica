// Desenho do veículo (vista lateral, frente à direita) pintado com a cor do cadastro.
//
// Cada modelo da frota tem desenho PRÓPRIO, traçado sobre fotos de perfil e nas medidas reais
// (comprimento, entre-eixos, altura, balanços): Onix, Polo, Spin, Sprinter e Master. Modelo fora
// do catálogo cai num desenho genérico da carroceria (hatch, sedan, SUV, picape, van, micro-ônibus,
// ônibus), escolhido pelo nome do modelo e, na falta, pelo tipo cadastrado.
//
// Sai como TEXTO SVG — a mesma fonte serve a tela (IlustracaoVeiculo) e o marcador de mapa
// (urlSvgVeiculo → data URL para o ícone do Google Maps / Leaflet). Nenhum texto do usuário entra
// no SVG: só números e cores calculadas aqui.
//
// Para acrescentar um modelo: traçar a silhueta numa caixa 240×110 (chão em y=102, frente à
// direita), registrar em DESENHOS e incluir o nome em MODELOS_FIEIS.

import type { FileiraDto, TipoVeiculo } from '@/features/veiculos/types';
import {
  CORES_FANTASIA,
  clarear,
  contornoDaCor,
  escurecer,
  luminancia,
  resolverCorVeiculo,
} from '@/features/veiculos/lib/corVeiculo';

type Roda = {
  x: number;
  y: number;
  /** Raio do pneu. */
  r: number;
  /** Raio da caixa de roda (recorte na carroceria). */
  arco: number;
  estilo: 'liga' | 'aco';
  raios?: number;
  /** Rodado duplo (traseira de van grande). */
  dupla?: boolean;
};

type Desenho = {
  nome: string;
  /** Altura (y) em que os arcos das rodas encontram a soleira. */
  base: number;
  /** [traseira, dianteira]. */
  rodas: [Roda, Roda];
  /** Silhueta pintada; `a(i)` desenha o arco da roda i (1 = dianteira, 0 = traseira). */
  corpo: (a: (i: 0 | 1) => string) => string;
  vidros: string[];
  /** Colunas pretas, base do retrovisor, grades — preto fosco. */
  pretos?: string[];
  /** Para-choques e frisos em plástico (recortados pela silhueta). */
  plasticos?: string[];
  /** Vãos de porta. */
  linhas: string[];
  /** Vinco da lataria (luz em cima, sombra embaixo). */
  vincos?: string[];
  macanetas: Array<[x: number, y: number, largura: number]>;
  farol: string;
  lanterna: string;
  retrovisor: string;
  detalhes?: string;
  /** Faixa de reflexo na lataria: [y, altura]. */
  brilho: [number, number];
  /** Faixa lateral da ambulância: [y, altura]. */
  faixa: [number, number];
  /** Giroflex: [x, y do teto, largura]. */
  sirene: [number, number, number];
  /** Cruz da ambulância. */
  emblema: [number, number];
  /** Selo de acessibilidade. */
  emblema2: [number, number];
  /** x da traseira e da frente (sombra no chão). */
  extensao: [number, number];
};

const PRETO = '#1b1e22';
const PLASTICO = '#2c3137';

// ── Modelos fiéis ───────────────────────────────────────────────────────────

const onix: Desenho = {
  nome: 'Chevrolet Onix',
  base: 90,
  rodas: [
    { x: 53.6, y: 86.6, r: 15.4, arco: 18.4, estilo: 'liga', raios: 5 },
    { x: 179.9, y: 86.6, r: 15.4, arco: 18.4, estilo: 'liga', raios: 5 },
  ],
  corpo: (a) =>
    'M24 88.5Q20.5 88 20.5 85.4L18 78.4Q16.8 74 17.2 69.7L19.4 57.5Q20.5 53.5 23.6 51.4L27.9 47.9' +
    'Q38 38 47 33.8Q50.5 32.2 53.5 32Q70 29.6 92 29Q115 28.9 128 29.8Q136 30.4 140.5 31.6L169 52.3' +
    'Q180 54 190.2 55.7Q202 58 208.6 61Q216 63.4 219 64.8Q222.4 67.5 222.8 72L223 82' +
    `Q223 88 219.5 89.5Q212 90.4 204 90.2${a(1)}${a(0)}L27 89.6Z`,
  vidros: [
    'M55.6 45.9L63.5 38Q68 33.6 75 32.6Q90 31.6 103 31.4L136.5 32.4Q138.5 32.6 139.5 33.8L159.5 53.6L101.5 49.6L72.8 47.9Z',
  ],
  pretos: [
    'M102.2 31.4L107.6 31.5L107.6 50.2L102.2 49.7Z',
    'M71.6 32.8L74.2 32.6L74.2 48.1L71.6 47.8Z',
    'M153 50.6L159.8 53.6L156 54.6Z',
    'M204 83L216 83.6Q218 86 216 88L207 88Z',
  ],
  linhas: [
    'M58.3 48.6Q59 62 66.5 71.5',
    'M102.6 50L101.8 88.6',
    'M159.3 54.3C161.2 64 160.6 77 159.6 88.5',
    'M72 88.7L159.4 88.9',
  ],
  vincos: ['M39.5 52.3Q100 54.6 158 56Q175 57.6 189 61.4', 'M78 80.5Q120 82 157 81.2'],
  macanetas: [
    [63.5, 53.4, 8],
    [106, 55.5, 9],
  ],
  farol: 'M189.4 61.5L207.3 64.9Q210.2 66.3 209.4 70.1L205 69.6Q197 66.6 190.4 63.7Z',
  lanterna: 'M21.2 52.3L39.3 50.9Q38 53.5 36 55L22.4 58.4Q20.8 55.4 21.2 52.3Z',
  retrovisor: 'M136 46.4L152.4 46.7Q156.8 47.2 157.4 50.6L155.6 53.6L140 52.6Q135.6 50.6 136 46.4Z',
  detalhes: '<rect x="162.4" y="66" width="3.4" height="1.3" rx=".6" fill="#e5e7eb" stroke="#9ca3af" stroke-width=".3"/>',
  brilho: [57.5, 4],
  faixa: [62.5, 4.5],
  sirene: [104, 29.1, 20],
  emblema: [129, 71],
  emblema2: [84, 71],
  extensao: [17, 223],
};

const polo: Desenho = {
  nome: 'Volkswagen Polo',
  base: 90.5,
  rodas: [
    { x: 49.3, y: 86.2, r: 15.8, arco: 18, estilo: 'liga', raios: 10 },
    { x: 185.3, y: 86.2, r: 15.8, arco: 18, estilo: 'liga', raios: 10 },
  ],
  corpo: (a) =>
    'M25 90.6Q21 90.2 19.6 86.8L17.9 83.2Q16.6 78 17 72.4L19.7 65.2Q20.6 58 21.5 54.5L23.3 49.1' +
    'Q26.5 43.5 29.6 40.1Q33 36.4 36.7 34.7L41.2 31.5Q46 30 51.1 29.3Q60 28.4 65.4 28.3Q80 27.6 92.3 27.5' +
    'Q108 27.8 119.3 28.8Q128 29.8 133.6 31.5L171.3 50.9Q181 52.4 191 54Q201 56.6 209 59.2' +
    'Q215 61 218 63Q221.4 65.6 221.5 68.8Q223 71 223 74.2L222.6 85Q222 90.5 218 92' +
    `L212 92.6Q207.5 92.6 205 91${a(1)}${a(0)}Z`,
  vidros: [
    'M50.2 45.9L58.5 35.5Q60.5 33 64 32.4L97.7 31.5L131.4 32Q133.2 32.3 134.2 33.6L162.3 52.3L104 48.3Z',
  ],
  pretos: [
    'M98.6 31.5L104 31.6L104.4 48.4L98.8 47.9Z',
    'M64.6 32.3L66.2 32.2L66.8 46.9L65.2 46.8Z',
    'M153.6 49.4L162.4 52.4L155.4 54.2Z',
    'M213.5 79.6L219.8 79.6L219.8 85L214.5 85Z',
    'M41.2 31.5L51 29.4L50.5 31.2L42.5 33.2Z',
  ],
  linhas: [
    'M57.9 47.4L60 65.2Q61.6 71 64.5 75.5Q67.5 80.5 72.6 86',
    'M104 48.3L105.8 86.8',
    'M163.2 52.7Q163.8 70 162.3 87.6',
    'M72.6 86.4L162.3 87.6',
  ],
  vincos: ['M36.7 52.7L162.3 58.4L203.6 63.4', 'M75 79.5Q120 81 162 80.2'],
  macanetas: [
    [61, 55.6, 11],
    [106.7, 58.6, 11.5],
  ],
  farol: 'M203.6 62.5L217.9 64Q219.6 66.5 219.4 70.6L209 70.6Q205.5 68.5 204.5 66.1Z',
  lanterna: 'M21.8 51.6L35.8 53.6L34 61.6L22.6 62.4Q21.2 57 21.8 51.6Z',
  retrovisor: 'M146.5 46.4L153.3 45.8Q155.1 47.5 155.1 50L154.2 53.6L148 53Q146 50 146.5 46.4Z',
  detalhes: '<rect x="41.2" y="51.8" width="12.6" height="7.3" rx="2" fill="none" stroke="LINHA" stroke-width=".5" opacity=".7"/>',
  brilho: [59, 4],
  faixa: [63.5, 4.5],
  sirene: [100, 27.6, 20],
  emblema: [133, 72],
  emblema2: [86, 72],
  extensao: [17, 223],
};

const spin: Desenho = {
  nome: 'Chevrolet Spin',
  base: 89.5,
  rodas: [
    { x: 55.2, y: 87, r: 15, arco: 18, estilo: 'liga', raios: 5 },
    { x: 178.9, y: 87, r: 15, arco: 18, estilo: 'liga', raios: 5 },
  ],
  corpo: (a) =>
    'M26 89.3Q21 88.6 19.6 85.5L18.6 80.5Q16.6 74 17.2 67.6L21.3 59L24.6 41.8Q27 33 30 30' +
    'Q33.5 26.3 38.1 25.7Q45 24 54.4 23.6L87 23.5Q115 23.8 130.2 25Q137 25.8 141 26.8L170 48.3' +
    'Q181 49.6 190 51Q204 53.8 212 57Q217 59 218.5 61.2Q221.8 64.5 222.1 69.8L223 78.4L222.6 85.5' +
    `Q221.8 89.2 217 90.2Q205 91 199 89.8${a(1)}${a(0)}L30 89.4Z`,
  vidros: [
    'M39 41.4L40.3 33.6Q41.6 29.6 47.6 29.1L67.9 28.5L68.8 45.7Z',
    'M70.9 28.5L105.4 28.5L105.4 48.2L70.9 46.1Z',
    'M110.8 28.6L138.9 30Q140 30.2 141 31.4L163.7 50.4L110.8 48.7Z',
  ],
  pretos: [
    'M67.9 28.5L70.9 28.5L70.9 46.1L68.8 45.9Z',
    'M105.4 28.5L110.8 28.6L110.8 48.7L105.4 48.3Z',
    'M157 47.4L164 50.5L159.8 51.6Z',
    'M209 77.5L217.8 78Q218.6 80.5 218 82.6L210.6 82.6Z',
  ],
  linhas: [
    'M69.2 46.4L69.6 73.8',
    'M105.8 49.3L105.6 87.6',
    'M158.3 51.5Q160 66 158.3 87.2',
    'M74 87.6L158.3 87.8',
  ],
  vincos: ['M34.5 52.8L158.5 57.8Q172 58.5 185 56.8', 'M80 79.5Q120 81 157 80.5'],
  macanetas: [
    [72.5, 54.6, 8.8],
    [109.6, 57.3, 9.7],
  ],
  farol: 'M185 53.5L212.5 58.6Q216 60 216.2 62.2L208 62.6Q196 59.2 186.2 55.4Z',
  lanterna: 'M24.2 44L32.2 45.3Q31 53.5 28 61L21.6 58.6Z',
  retrovisor: 'M143.2 43.4L154.8 42.6Q158 43.4 158.3 47L157 50.4L146 50Q142.6 47.2 143.2 43.4Z',
  detalhes: '<circle cx="46.3" cy="56.4" r="3.4" fill="none" stroke="LINHA" stroke-width=".5" opacity=".7"/>',
  brilho: [55, 4.5],
  faixa: [61.5, 4.5],
  sirene: [96, 23.5, 22],
  emblema: [134, 70],
  emblema2: [86, 70],
  extensao: [17, 223],
};

const sprinter: Desenho = {
  nome: 'Mercedes-Benz Sprinter',
  base: 93,
  rodas: [
    { x: 64.4, y: 91.1, r: 10.9, arco: 14, estilo: 'aco', dupla: true },
    { x: 196.4, y: 91.1, r: 10.9, arco: 14, estilo: 'aco' },
  ],
  corpo: (a) =>
    'M17 93.2Q13.4 93 13.2 89.5L13 24Q13 18.2 18.5 18L150 18Q160 18 164.5 20.5Q169 23 171 26' +
    'L195.5 56Q199 57.6 204 58.5Q216 60.8 222 62.6Q225.4 63.8 225.8 67L227 78L227.4 90' +
    `Q227.2 94 223.8 94.2L213 94Q211.5 93.6 211 93.2${a(1)}${a(0)}L22 93.2Z`,
  vidros: [
    'M22.5 30Q22.5 27.8 24.7 27.8L150.5 27.8L150.5 53L24.7 53Q22.5 53 22.5 50.8Z',
    'M156.5 27.8L170.2 27.4Q171.6 27.6 172.4 28.8L185 46L179.6 53L156.5 53Z',
  ],
  pretos: [
    'M150.5 27.8L156.5 27.8L156.5 53L150.5 53Z',
    'M179.6 53L185 46L192.6 56.2L180.6 56.2Z',
    'M186 40.5L191 39.5Q193.2 40 193 43L192.6 53.5Q192.4 55.5 190.2 55.4L187.5 55Q185.6 54.6 185.6 52.6Z',
  ],
  plasticos: [
    'M13 76.5L182.6 76.5L182.6 82L13 82Z',
    'M210.5 95L210.5 86.5Q210.5 84 213 84L228 84L228 95Z',
    'M12 85L24 85Q26.5 85 26.5 87.5L26.5 94L12 94Z',
  ],
  linhas: [
    'M193 57.2Q192 70 185.5 79',
    'M152.8 53.5L152.8 92.6',
    'M148 55L148 92.6',
    'M110 55L110 92.6',
    'M110 55.8L23 55.8',
  ],
  macanetas: [
    [161, 59.5, 6],
    [140.5, 60, 6],
  ],
  farol: 'M206 59.6L221.5 62.7Q224.8 64 225 67.4L216 68.2Q210 65 206.4 61.4Z',
  lanterna: 'M13 60L15.8 60L15.8 75.6L13 75.6Z',
  retrovisor: '',
  detalhes:
    '<path d="M54 28.2V52.6M86 28.2V52.6M118 28.2V52.6" stroke="#fff" stroke-opacity=".16" stroke-width=".8"/>' +
    '<rect x="176" y="78.3" width="3.6" height="1.9" rx=".5" fill="#f59e0b"/>' +
    '<rect x="98" y="78.3" width="3.6" height="1.9" rx=".5" fill="#f59e0b"/>',
  brilho: [58, 4],
  faixa: [69, 5.5],
  sirene: [140, 18, 26],
  emblema: [128, 63.5],
  emblema2: [40, 63.5],
  extensao: [13, 227],
};

const master: Desenho = {
  nome: 'Renault Master',
  base: 91.5,
  rodas: [
    { x: 45.4, y: 89.7, r: 12.3, arco: 15.5, estilo: 'aco' },
    { x: 194.9, y: 89.7, r: 12.3, arco: 15.5, estilo: 'aco' },
  ],
  corpo: (a) =>
    'M17 91.5Q14.3 91 14.3 89.5L13 83.4L13.7 70.1L15 46.4L17 23.9Q17.6 16.2 21 14.4Q23 13.5 26 13.4' +
    'L102.5 12.2L148.5 11.9Q154.5 11.9 158.1 13.2Q162 15 164.8 17.1L173.4 27L184 41.9Q186.5 46.5 188.8 49.2' +
    'Q195 50 200.8 50.6Q212 51.8 218.8 53.6Q221.2 55 222.1 57.6L224.4 67.6L227 77.6L226.3 89.6' +
    `Q225 93 220.4 93.3L212.5 92.9Q211 92.4 210.6 91.8${a(1)}${a(0)}Z`,
  vidros: [
    'M103.1 27Q103.1 25 105.1 25L137.9 25Q139.9 25 139.9 27L139.9 49Q139.9 51 137.9 51L105.1 51Q103.1 51 103.1 49Z',
    'M63.7 27Q63.7 25 65.7 25L98.5 25Q100.5 25 100.5 27L100.5 49Q100.5 51 98.5 51L65.7 51Q63.7 51 63.7 49Z',
    'M24.4 27.5Q24.4 25 26.9 25L59.1 25Q61.1 25 61.1 27L61.1 49Q61.1 51 59.1 51L26.4 51Q24.4 51 24.4 49Z',
    'M145.4 27.6Q145.4 25.7 147.3 25.7L160.2 25.5Q161.6 25.5 162.3 26.6L176.4 52.6Q176.9 54.4 175 54L147 45.6Q145.3 45.2 145.2 43.4Z',
  ],
  pretos: [
    'M181 40L186.5 38.8Q188.6 39.4 188.2 42L186 53.4Q185.4 55.4 183 55L180.6 54.6Q179 54 179.4 52Z',
  ],
  plasticos: [
    'M23.7 77L166 77Q169 77 170.5 79.8L172 82.5L23.7 82.5Z',
    'M210.5 94L210.5 83Q210.5 80.5 213 80.5L228 80.5L228 94Z',
    'M12 82L24 82Q26.5 82 26.5 84.5L26.5 92L12 92Z',
  ],
  linhas: [
    'M179.1 55.6L178.8 85',
    'M143.7 26L143.7 89.5',
    'M101.8 52.7L101.8 90.6',
  ],
  vincos: ['M24 58Q100 59.5 176 60.5'],
  macanetas: [[148.5, 58.6, 9.6]],
  farol: 'M212.3 54.5L222.2 56.4Q224 60 223.4 63L220.4 65.5Q214 62.5 210.6 59.4Z',
  lanterna: 'M13.8 53.8L16.3 53.8L15.2 74.6L13.7 74.6Z',
  retrovisor: '',
  brilho: [56, 4],
  faixa: [69.5, 5.5],
  sirene: [132, 11.9, 26],
  emblema: [122, 63.5],
  emblema2: [42, 63.5],
  extensao: [13, 227],
};

// ── Genéricos por carroceria ────────────────────────────────────────────────

const sedan: Desenho = {
  ...onix,
  nome: 'Sedan',
  rodas: [
    { x: 60, y: 86.6, r: 15.2, arco: 18.2, estilo: 'liga', raios: 5 },
    { x: 181, y: 86.6, r: 15.2, arco: 18.2, estilo: 'liga', raios: 5 },
  ],
  corpo: (a) =>
    'M24 88.5Q20.5 88 20.3 85L18.4 78Q17 73 17.6 68L19 55Q19.8 50.5 24 49.2L40 47.4Q45 46.8 50 43.4' +
    'L62 35Q68 31 76 30.2Q100 29 120 29.3Q134 30 140.5 31.8L169 52.3Q180 54 190.2 55.7Q202 58 208.6 61' +
    'Q216 63.4 219 64.8Q222.4 67.5 222.8 72L223 82Q223 88 219.5 89.5Q212 90.4 204 90.2' +
    `${a(1)}${a(0)}L27 89.6Z`,
  vidros: ['M60 46.8L70 37.2Q74 33.4 80 33L136 32.4Q138.5 32.6 139.5 33.8L159.5 53.6L102 49.8Z'],
  pretos: [
    'M102.2 32.6L107.6 32.6L107.6 50.2L102.2 49.8Z',
    'M153 50.6L159.8 53.6L156 54.6Z',
    'M204 83L216 83.6Q218 86 216 88L207 88Z',
  ],
  linhas: [
    'M70 47.8Q72 62 74.5 70.5',
    'M102.6 50L101.8 88.6',
    'M159.3 54.3C161.2 64 160.6 77 159.6 88.5',
    'M78.5 88.7L159.4 88.9',
  ],
  vincos: ['M34 51.4Q100 54.6 158 56Q175 57.6 189 61.4'],
  macanetas: [
    [74, 53.8, 8.5],
    [106, 55.5, 9],
  ],
  lanterna: 'M19.3 50.5L34 48.8L32 53.6L20.2 55.6Z',
  detalhes: '',
  emblema: [131, 71],
  emblema2: [88, 71],
};

const suv: Desenho = {
  nome: 'SUV',
  base: 89,
  rodas: [
    { x: 55, y: 85.5, r: 16.5, arco: 19.8, estilo: 'liga', raios: 5 },
    { x: 181, y: 85.5, r: 16.5, arco: 19.8, estilo: 'liga', raios: 5 },
  ],
  corpo: (a) =>
    'M25 88.5Q20.5 88 20 84.5L18.6 77Q17.4 71 18 64L20 46Q21.5 34 27 29.5Q31 26.6 38 26.2L118 25.2' +
    'Q130 25.2 136 28.5L163 49Q176 50.6 190 52.2Q204 54 212 57Q217.5 59 219.5 62Q222.5 66 222.8 72' +
    `L223 82Q223 88 219 89Q208 89.6 203 89.2${a(1)}${a(0)}L29 88.8Z`,
  vidros: [
    'M27.5 45L29.4 34.6Q30.8 30.2 36 30L62 29.4L62.5 47.2Z',
    'M65 29.4L104 28.8L104 48.8L65 47.4Z',
    'M109 28.8L132.5 29.4Q134.2 29.6 135.2 30.8L157 50.4L109 49Z',
  ],
  pretos: ['M151 47.6L157.4 50.4L153.8 51.4Z'],
  plasticos: ['M40 85L170 85L170 89.5L40 89.5Z'],
  linhas: ['M62.8 47.8L63.5 66', 'M106.5 49L106 87.5', 'M155 50.5Q157 64 155.5 87'],
  vincos: ['M28 53Q100 55 155 56.5Q175 57.5 190 55.8'],
  macanetas: [
    [70, 54.5, 9],
    [111, 56, 9.5],
  ],
  farol: 'M190 53.2L211.5 57.4Q216 59.4 216.2 62.5L207 62.8Q197 59.6 191 55.4Z',
  lanterna: 'M19.6 46L29 45L28 55L19.2 55.8Z',
  retrovisor: 'M140 40L152 39.4Q155.2 40.2 155.4 44L154 47.4L143 47Q139.6 44 140 40Z',
  detalhes: '<path d="M40 24.6L118 23.6" stroke="#2c3137" stroke-width="1.6" stroke-linecap="round"/>',
  brilho: [57, 4],
  faixa: [61, 4.5],
  sirene: [98, 25.2, 22],
  emblema: [131, 70],
  emblema2: [84, 70],
  extensao: [18, 223],
};

const picape: Desenho = {
  nome: 'Picape',
  base: 89.5,
  rodas: [
    { x: 52, y: 86, r: 16, arco: 19.4, estilo: 'liga', raios: 6 },
    { x: 183, y: 86, r: 16, arco: 19.4, estilo: 'liga', raios: 6 },
  ],
  corpo: (a) =>
    'M24 89Q19.8 88.6 19.4 85L18.4 78Q17.6 72 18.4 66L19.6 50Q20 47.4 23 47.4L88 47.4L91 33.4' +
    'Q93 29 99 28.8L128 28.3Q137 28.3 142 31.6L166 50.6Q180 52.2 192 53.8Q205 55.8 212.5 58.4' +
    `Q218 60.4 220 63.4Q222.6 67 222.8 73L223 83Q223 88.6 219 89.4Q210 90 204 89.8${a(1)}${a(0)}L28 89.4Z`,
  vidros: [
    'M95 49.4L96 34.4Q97 31.6 101 31.6L124 31.4L124 49.6Z',
    'M129 49.6L129 31.4L134.5 31.4Q138 31.6 140.5 33.6L160 51Z',
  ],
  pretos: ['M154 48.2L160.4 51.2L156.8 52.2Z'],
  linhas: ['M91.2 49L91.2 86.5', 'M126.5 49.6L126 88', 'M162 51.6Q164 66 162.5 88', 'M22.2 49.6L22.2 77'],
  vincos: ['M20 51.2L88 51.2', 'M95 55.5L160 57.5Q178 58.4 192 57'],
  macanetas: [
    [110, 55.5, 9],
    [147, 56.5, 9],
  ],
  farol: 'M192 55L213.5 59Q218 61 218.2 64.2L209 64.6Q199 61.4 193 57.2Z',
  lanterna: 'M18.8 52L23.8 52L23.8 64L18.6 64Z',
  retrovisor: 'M145 44L156 43.4Q159 44.2 159.2 47.8L158 51L148 50.6Q144.6 47.8 145 44Z',
  brilho: [58, 4],
  faixa: [62, 4.5],
  sirene: [104, 28.4, 18],
  emblema: [142, 70],
  emblema2: [55, 64],
  extensao: [18, 223],
};

const microOnibus: Desenho = {
  nome: 'Micro-ônibus',
  base: 92,
  rodas: [
    { x: 58, y: 89, r: 13, arco: 16.5, estilo: 'aco', dupla: true },
    { x: 190, y: 89, r: 13, arco: 16.5, estilo: 'aco' },
  ],
  corpo: (a) =>
    'M12 92Q9.4 91.6 9.2 88.6L8 20Q8 14 14 14L198 14Q208 14 213 20Q217 25 219.5 32L225.5 50' +
    `Q227.6 55 227.8 62L228 88Q228 92 224 92${a(1)}${a(0)}Z`,
  vidros: [
    'M186 50L186 22L206 22Q209.5 22 211.5 26L222 50Z',
    ...[14, 47.6, 81.2, 114.8, 148.4].map(
      (x) => `M${x} 24Q${x} 22 ${x + 2} 22L${x + 29.6} 22Q${x + 31.6} 22 ${x + 31.6} 24L${x + 31.6} 46Q${x + 31.6} 48 ${x + 29.6} 48L${x + 2} 48Q${x} 48 ${x} 46Z`,
    ),
  ],
  pretos: [
    'M224 30Q231 30 231.5 36L231.5 42L230.5 42L230.5 36Q230 31 224 31Z',
    'M229.4 40L233 40L233 49L229.4 49Z',
  ],
  plasticos: ['M8 74L228 74L228 78L8 78Z', 'M214 84L229 84L229 93L214 93Z', 'M7 84L22 84L22 93L7 93Z'],
  linhas: ['M184 50L184 88'],
  macanetas: [[176, 56, 6]],
  farol: 'M220 60L227.6 60L227.8 67L221 67Z',
  lanterna: 'M8.2 58L11 58L11 74L8.2 74Z',
  retrovisor: '',
  brilho: [54, 4],
  faixa: [66, 5],
  sirene: [168, 14, 26],
  emblema: [110, 61],
  emblema2: [30, 61],
  extensao: [8, 228],
};

const onibus: Desenho = {
  nome: 'Ônibus',
  base: 93,
  rodas: [
    { x: 62, y: 91, r: 11, arco: 14, estilo: 'aco', dupla: true },
    { x: 186, y: 91, r: 11, arco: 14, estilo: 'aco' },
  ],
  corpo: (a) =>
    'M8 93Q4.6 92.6 4.4 89L4 38Q4 30 12 30L224 30Q232 30 233.4 38L235.6 60L236 89Q236 93 232 93' +
    `${a(1)}${a(0)}Z`,
  vidros: [
    'M216 60L216 36L227 36Q231 36 232 40L234 60Z',
    ...[0, 1, 2, 3, 4, 5, 6].map((i) => {
      const x = 10 + i * 29;
      return `M${x} 37.5Q${x} 36 ${x + 1.5} 36L${x + 24.5} 36Q${x + 26} 36 ${x + 26} 37.5L${x + 26} 56.5Q${x + 26} 58 ${x + 24.5} 58L${x + 1.5} 58Q${x} 58 ${x} 56.5Z`;
    }),
  ],
  pretos: ['M214 31.6L230 31.6L230 34.4L214 34.4Z'],
  plasticos: ['M4 77L236 77L236 80L4 80Z', 'M222 86L237 86L237 94L222 94Z', 'M3 86L18 86L18 94L3 94Z'],
  linhas: ['M214 60L214 90'],
  macanetas: [],
  farol: 'M229 72L236 72L236 77L229 77Z',
  lanterna: 'M4.3 62L7.5 62L7.5 76L4.3 76Z',
  retrovisor: '',
  brilho: [63, 4],
  faixa: [67, 5],
  sirene: [180, 30, 26],
  emblema: [120, 68],
  emblema2: [30, 68],
  extensao: [4, 236],
};

const DESENHOS = {
  onix,
  polo,
  spin,
  sprinter,
  master,
  hatch: { ...onix, nome: 'Hatch' },
  sedan,
  suv,
  picape,
  minivan: { ...spin, nome: 'Minivan' },
  van: { ...master, nome: 'Van' },
  microOnibus,
  onibus,
} satisfies Record<string, Desenho>;

export type ChaveDesenho = keyof typeof DESENHOS;

// Nome do modelo → desenho próprio. Conferido antes dos genéricos.
const MODELOS_FIEIS: ReadonlyArray<[RegExp, ChaveDesenho]> = [
  [/\bonix\b(?!\s*(plus|sedan))/, 'onix'],
  [/\bpolo\b(?!\s*sedan)/, 'polo'],
  [/\bspin\b/, 'spin'],
  [/\bsprinter\b/, 'sprinter'],
  [/\bmaster\b/, 'master'],
];

const GENERICOS: ReadonlyArray<[RegExp, ChaveDesenho]> = [
  [/\b(onix\s*(plus|sedan)|polo\s*sedan)\b/, 'sedan'],
  [/\b(volare|micro|senior|w[- ]?9|v[- ]?8l?)\b/, 'microOnibus'],
  [/\b(onibus|torino|viale|paradiso|apache)\b/, 'onibus'],
  [/\b(ducato|boxer|jumper|daily|transit|trafic|kombi|besta|topic|vito|furgao|h1|expert|jumpy)\b/, 'van'],
  [/\b(strada|saveiro|montana|toro|hilux|s10|ranger|amarok|oroch|frontier|l200|maverick|rampage|triton)\b/, 'picape'],
  [/\b(corolla\s*cross|duster|renegade|compass|tracker|creta|t-?cross|kicks|hr-?v|ecosport|captur|taos|tiguan|sw4|pajero|2008|3008|nivus|pulse|fastback|commander|trailblazer|tucson|sportage|wr-?v|kardian|territory)\b/, 'suv'],
  [/\b(zafira|livina|meriva|doblo|idea|picasso|touran)\b/, 'minivan'],
  [/\b(logan|voyage|prisma|cronos|virtus|corolla|versa|sentra|civic|city|cobalt|hb20s|siena|fluence|jetta|cruze|linea|vectra|elantra|cerato)\b/, 'sedan'],
];

const PADRAO_POR_TIPO: Record<TipoVeiculo, ChaveDesenho> = {
  Carro: 'hatch',
  Van: 'van',
  MicroOnibus: 'microOnibus',
  Onibus: 'onibus',
  Ambulancia: 'van',
  Outro: 'van',
};

function normalizar(t: string): string {
  return t
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();
}

export type DesenhoEscolhido = {
  chave: ChaveDesenho;
  nome: string;
  /** true = desenho do próprio modelo; false = genérico da carroceria. */
  fiel: boolean;
};

export function escolherDesenho(
  tipo: TipoVeiculo,
  modelo?: string | null,
  fabricante?: string | null,
): DesenhoEscolhido {
  const texto = normalizar(`${fabricante ?? ''} ${modelo ?? ''}`);
  const fiel = MODELOS_FIEIS.find(([re]) => re.test(texto))?.[1];
  if (fiel) return { chave: fiel, nome: DESENHOS[fiel].nome, fiel: true };
  const chave = GENERICOS.find(([re]) => re.test(texto))?.[1] ?? PADRAO_POR_TIPO[tipo] ?? 'van';
  return { chave, nome: DESENHOS[chave].nome, fiel: false };
}

/** "SPRINTER ADAPTADA", "Master acessível", "van PcD"… */
export function modeloAdaptado(modelo?: string | null): boolean {
  return /adaptad|acessiv|cadeirant|\bpcd\b/.test(normalizar(modelo ?? ''));
}

/** Adaptado = o nome diz ("… ADAPTADA") ou o layout tem assento de cadeirante em uso. */
export function veiculoAdaptado(modelo: string, fileiras?: FileiraDto[]): boolean {
  return (
    modeloAdaptado(modelo) ||
    Boolean(fileiras?.some((f) => f.assentos.some((a) => a.tipo === 'Cadeirante' && !a.bloqueado)))
  );
}

// ── Montagem do SVG ─────────────────────────────────────────────────────────

export type OpcoesDesenho = {
  tipo: TipoVeiculo;
  modelo?: string | null;
  fabricante?: string | null;
  /** Texto da cor como cadastrado ("Branco", "Prata metálico", "#1e3a8a"…). */
  cor: string;
  /** Selo de acessibilidade na lateral. Se omitido, deduzido do nome do modelo. */
  adaptado?: boolean;
  /** Largura em px (só para o marcador de mapa; na tela o SVG ocupa o container). */
  largura?: number;
};

function hash(texto: string): string {
  let h = 5381;
  for (let i = 0; i < texto.length; i++) h = ((h << 5) + h + texto.charCodeAt(i)) >>> 0;
  return h.toString(36);
}

function arcoDaRoda(base: number, roda: Roda): string {
  const dx = Math.sqrt(roda.arco ** 2 - (base - roda.y) ** 2);
  return `L${(roda.x + dx).toFixed(2)} ${base}A${roda.arco} ${roda.arco} 0 1 0 ${(roda.x - dx).toFixed(2)} ${base}`;
}

function caixaDeRoda(base: number, roda: Roda): string {
  const dx = Math.sqrt(roda.arco ** 2 - (base - roda.y) ** 2);
  return `M${(roda.x - dx).toFixed(2)} ${base}A${roda.arco} ${roda.arco} 0 1 1 ${(roda.x + dx).toFixed(2)} ${base}Z`;
}

function svgRoda({ x, y, r, estilo, raios = 5, dupla }: Roda): string {
  const partes = [`<circle cx="${x}" cy="${y}" r="${r}" fill="#16181b"/>`];
  partes.push(`<circle cx="${x}" cy="${y}" r="${(r * 0.9).toFixed(2)}" fill="none" stroke="#2b2f34" stroke-width=".7"/>`);
  if (estilo === 'aco') {
    const aro = r * 0.64;
    partes.push(`<circle cx="${x}" cy="${y}" r="${aro.toFixed(2)}" fill="${dupla ? '#8d949c' : '#b4bac1'}" stroke="#6b7280" stroke-width=".5"/>`);
    partes.push(`<circle cx="${x}" cy="${y}" r="${(aro * 0.78).toFixed(2)}" fill="none" stroke="#7c838b" stroke-width=".5"/>`);
    for (let i = 0; i < 8; i++) {
      const ang = (i * Math.PI) / 4;
      partes.push(
        `<circle cx="${(x + Math.cos(ang) * aro * 0.62).toFixed(2)}" cy="${(y + Math.sin(ang) * aro * 0.62).toFixed(2)}" r="${(r * 0.045).toFixed(2)}" fill="#50565d"/>`,
      );
    }
    partes.push(`<circle cx="${x}" cy="${y}" r="${(aro * 0.34).toFixed(2)}" fill="#d3d8dd" stroke="#8a9199" stroke-width=".4"/>`);
    return partes.join('');
  }
  const aro = r * 0.68;
  partes.push(`<circle cx="${x}" cy="${y}" r="${aro.toFixed(2)}" fill="#353a40"/>`);
  const n = raios;
  const d: string[] = [];
  for (let i = 0; i < n; i++) {
    const base = (i * 2 * Math.PI) / n - Math.PI / 2;
    for (const off of n <= 6 ? [-0.13, 0.13] : [0]) {
      const ang = base + off;
      const x1 = x + Math.cos(base) * aro * 0.26;
      const y1 = y + Math.sin(base) * aro * 0.26;
      const x2 = x + Math.cos(ang) * aro * 0.93;
      const y2 = y + Math.sin(ang) * aro * 0.93;
      d.push(`M${x1.toFixed(2)} ${y1.toFixed(2)}L${x2.toFixed(2)} ${y2.toFixed(2)}`);
    }
  }
  partes.push(`<path d="${d.join('')}" stroke="#cfd4da" stroke-width="${(r * (n <= 6 ? 0.075 : 0.07)).toFixed(2)}" stroke-linecap="round"/>`);
  partes.push(`<circle cx="${x}" cy="${y}" r="${aro.toFixed(2)}" fill="none" stroke="#b9bfc6" stroke-width="${(r * 0.07).toFixed(2)}"/>`);
  partes.push(`<circle cx="${x}" cy="${y}" r="${(aro * 0.26).toFixed(2)}" fill="#d9dde2" stroke="#8f969d" stroke-width=".4"/>`);
  return partes.join('');
}

function avermelhada(hex: string): boolean {
  const n = parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return r > 120 && r > g * 1.6 && r > b * 1.6;
}

function seloAcessibilidade(x: number, y: number, t: number): string {
  const h = t / 2;
  return (
    `<g transform="translate(${x - h} ${y - h})">` +
    `<rect width="${t}" height="${t}" rx="${t * 0.18}" fill="#1d4ed8" stroke="#fff" stroke-width=".6"/>` +
    `<g transform="scale(${t / 12})" fill="none" stroke="#fff" stroke-width="1.15" stroke-linecap="round" stroke-linejoin="round">` +
    '<circle cx="6.4" cy="2.4" r="1" fill="#fff" stroke="none"/>' +
    '<path d="M6 4.2V7.4H8.6L9.7 9.9"/><path d="M6 5.6H8"/><path d="M4.5 6.2A2.9 2.9 0 1 0 8 9.8"/>' +
    '</g></g>'
  );
}

function cruz(x: number, y: number, r: number, corFaixa: string): string {
  const b = r * 0.32;
  const l = r * 0.72;
  return (
    `<circle cx="${x}" cy="${y}" r="${r}" fill="#fff" stroke="${corFaixa}" stroke-width=".8"/>` +
    `<path d="M${x - b} ${y - l}H${x + b}V${y - b}H${x + l}V${y + b}H${x + b}V${y + l}H${x - b}V${y + b}H${x - l}V${y - b}H${x - b}Z" fill="#dc2626"/>`
  );
}

export function svgVeiculo(opcoes: OpcoesDesenho): string {
  const { chave } = escolherDesenho(opcoes.tipo, opcoes.modelo, opcoes.fabricante);
  const d: Desenho = DESENHOS[chave];
  const cor = resolverCorVeiculo(opcoes.cor);
  const hex = cor.hex;
  const ambulancia = opcoes.tipo === 'Ambulancia';
  const adaptado = opcoes.adaptado ?? modeloAdaptado(opcoes.modelo);

  const id = `vx${hash(`${chave}|${hex}|${cor.fantasia}`)}`;
  const contorno = contornoDaCor(hex);
  const linha = escurecer(hex, luminancia(hex) > 0.6 ? 0.3 : 0.4);
  const corpo = d.corpo((i) => arcoDaRoda(d.base, d.rodas[i]));

  const pintura = cor.fantasia
    ? `<linearGradient id="${id}c" x1="0" y1="0" x2="1" y2="0">${CORES_FANTASIA.map(
        (c, i) => `<stop offset="${i / (CORES_FANTASIA.length - 1)}" stop-color="${c}"/>`,
      ).join('')}</linearGradient>`
    : `<linearGradient id="${id}c" x1="0" y1="0" x2="0" y2="1">` +
      `<stop offset="0" stop-color="${clarear(hex, 0.3)}"/>` +
      `<stop offset=".4" stop-color="${hex}"/>` +
      `<stop offset=".8" stop-color="${escurecer(hex, 0.12)}"/>` +
      `<stop offset="1" stop-color="${escurecer(hex, 0.3)}"/>` +
      '</linearGradient>';

  const [xt, xf] = d.extensao;
  const corFaixa = avermelhada(hex) ? '#ffffff' : '#d32f2f';
  const partes: string[] = [];

  partes.push(
    '<defs>' +
      pintura +
      `<linearGradient id="${id}v" x1="0" y1="0" x2="1" y2="1">` +
      '<stop offset="0" stop-color="#8a9aab"/><stop offset=".38" stop-color="#3b4a5a"/>' +
      '<stop offset=".39" stop-color="#2a3642"/><stop offset="1" stop-color="#141b23"/></linearGradient>' +
      `<clipPath id="${id}k"><path d="${corpo}"/></clipPath>` +
      '</defs>',
  );

  // Sombra no chão.
  partes.push(
    `<ellipse cx="${(xt + xf) / 2}" cy="102.4" rx="${(xf - xt) / 2 + 2}" ry="2.6" fill="#000" opacity=".16"/>`,
  );

  // Caixas de roda (fundo escuro), carroceria, pneus.
  partes.push(d.rodas.map((r) => `<path d="${caixaDeRoda(d.base, r)}" fill="#0f1113"/>`).join(''));
  partes.push(
    `<path d="${corpo}" fill="url(#${id}c)" stroke="${contorno}" stroke-width=".9" stroke-linejoin="round"/>`,
  );

  // Dentro da silhueta: reflexo, plásticos, faixa da ambulância.
  const dentro: string[] = [
    `<rect x="0" y="${d.brilho[0]}" width="240" height="${d.brilho[1]}" fill="#fff" opacity=".13"/>`,
  ];
  for (const p of d.plasticos ?? []) dentro.push(`<path d="${p}" fill="${PLASTICO}"/>`);
  if (ambulancia) {
    dentro.push(`<rect x="0" y="${d.faixa[0]}" width="240" height="${d.faixa[1]}" fill="${corFaixa}"/>`);
  }
  partes.push(`<g clip-path="url(#${id}k)">${dentro.join('')}</g>`);

  // Vincos e vãos de porta.
  for (const v of d.vincos ?? []) {
    partes.push(`<path d="${v}" fill="none" stroke="#fff" stroke-opacity=".32" stroke-width=".7"/>`);
    partes.push(
      `<path d="${v}" transform="translate(0 .9)" fill="none" stroke="#000" stroke-opacity=".14" stroke-width=".7"/>`,
    );
  }
  partes.push(
    `<path d="${d.linhas.join('')}" fill="none" stroke="${linha}" stroke-opacity=".75" stroke-width=".65" stroke-linecap="round"/>`,
  );

  // Vidros, colunas pretas, retrovisor.
  for (const v of d.vidros) {
    partes.push(`<path d="${v}" fill="url(#${id}v)" stroke="#0e1318" stroke-width=".6" stroke-linejoin="round"/>`);
  }
  for (const p of d.pretos ?? []) partes.push(`<path d="${p}" fill="${PRETO}"/>`);
  if (d.retrovisor) {
    partes.push(`<path d="${d.retrovisor}" fill="url(#${id}c)" stroke="${contorno}" stroke-width=".6"/>`);
  }

  // Maçanetas, faróis, lanternas, detalhes do modelo.
  for (const [x, y, w] of d.macanetas) {
    partes.push(
      `<rect x="${x}" y="${y - 1.1}" width="${w}" height="2.2" rx="1.1" fill="${escurecer(hex, 0.16)}" stroke="${linha}" stroke-width=".4"/>`,
    );
  }
  partes.push(`<path d="${d.farol}" fill="#eef2f7" stroke="#7b838c" stroke-width=".5" stroke-linejoin="round"/>`);
  partes.push(`<path d="${d.lanterna}" fill="#c81e1e" stroke="#7f1d1d" stroke-width=".5" stroke-linejoin="round"/>`);
  if (d.detalhes) partes.push(d.detalhes.replaceAll('LINHA', linha));

  if (ambulancia) {
    const [sx, sy, sw] = d.sirene;
    partes.push(
      `<rect x="${sx}" y="${sy - 4}" width="${sw}" height="4.2" rx="1.2" fill="#dc2626" stroke="#7f1d1d" stroke-width=".5"/>` +
        `<rect x="${sx + sw / 2 - 2.6}" y="${sy - 4}" width="5.2" height="4.2" fill="#f8fafc"/>` +
        `<rect x="${sx + 1}" y="${sy - 3.4}" width="${sw - 2}" height="1.1" rx=".5" fill="#fff" opacity=".45"/>`,
    );
    partes.push(cruz(d.emblema[0], d.emblema[1], d.faixa[1] > 5 ? 6 : 5, corFaixa === '#ffffff' ? '#991b1b' : corFaixa));
  }
  if (adaptado) partes.push(seloAcessibilidade(d.emblema2[0], d.emblema2[1], 9));

  partes.push(d.rodas.map(svgRoda).join(''));

  const tamanho = opcoes.largura
    ? ` width="${opcoes.largura}" height="${Math.round((opcoes.largura * 110) / 240)}"`
    : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 110"${tamanho}>${partes.join('')}</svg>`;
}

/** Data URL do desenho — pronto para `icon.url` do Google Maps ou `<img src>`. */
export function urlSvgVeiculo(opcoes: OpcoesDesenho): string {
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svgVeiculo(opcoes))}`;
}
