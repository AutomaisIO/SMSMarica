// Cor do veículo → cor de pintura do desenho.
//
// O cadastro guarda a cor como TEXTO (é o que está no documento do veículo e o que a frota já
// tem gravado: "Branco", "CINZA", "AZUL"…). A paleta do formulário oferece as 16 cores da tabela
// do RENAVAM — as mesmas que saem no CRLV —, mas o texto livre continua valendo: "Prata metálico",
// "Azul marinho", "Cinza grafite" e até um hex (#1e3a8a) são reconhecidos aqui. O que não for
// reconhecido pinta de cinza neutro, sinalizado para o formulário avisar.

export type CorRenavam = {
  /** Nome como aparece no CRLV (tabela de cores do RENAVAM). */
  nome: string;
  hex: string;
};

/** Tabela de cores do RENAVAM, na ordem do documento. */
export const CORES_RENAVAM: readonly CorRenavam[] = [
  { nome: 'Amarela', hex: '#f2c12e' },
  { nome: 'Azul', hex: '#1f4fa3' },
  { nome: 'Bege', hex: '#d9c6a5' },
  { nome: 'Branca', hex: '#f4f5f6' },
  { nome: 'Cinza', hex: '#6e737a' },
  { nome: 'Dourada', hex: '#c8a24a' },
  { nome: 'Grená', hex: '#7a1e2c' },
  { nome: 'Laranja', hex: '#e5711d' },
  { nome: 'Marrom', hex: '#6b4a33' },
  { nome: 'Prata', hex: '#b9bec5' },
  { nome: 'Preta', hex: '#1d1f22' },
  { nome: 'Rosa', hex: '#e58fb0' },
  { nome: 'Roxa', hex: '#5f3a8f' },
  { nome: 'Verde', hex: '#2f7d3b' },
  { nome: 'Vermelha', hex: '#c62a2a' },
  { nome: 'Fantasia', hex: '#c62a2a' },
];

/** Cores de uma pintura "Fantasia" (mais de uma cor / adesivada). */
export const CORES_FANTASIA = ['#c62a2a', '#f2c12e', '#2f7d3b', '#1f4fa3'] as const;

const COR_NEUTRA = '#9ca3af';

export type CorResolvida = {
  hex: string;
  /** Cor da tabela do RENAVAM a que o texto corresponde (destaca a amostra na paleta). */
  base: CorRenavam | null;
  reconhecida: boolean;
  fantasia: boolean;
};

function porNome(nome: string): CorRenavam {
  return CORES_RENAVAM.find((c) => c.nome === nome)!;
}

// Radical (sem acento, minúsculo) → cor. Radical casa "Branco" e "Branca", "Preto" e "Preta".
const RADICAIS: ReadonlyArray<[string, string]> = [
  ['amarel', 'Amarela'],
  ['azul', 'Azul'],
  ['bege', 'Bege'],
  ['branc', 'Branca'],
  ['cinz', 'Cinza'],
  ['dourad', 'Dourada'],
  ['grena', 'Grená'],
  ['laranj', 'Laranja'],
  ['marrom', 'Marrom'],
  ['prat', 'Prata'],
  ['pret', 'Preta'],
  ['rosa', 'Rosa'],
  ['rox', 'Roxa'],
  ['verd', 'Verde'],
  ['vermelh', 'Vermelha'],
  ['fantasia', 'Fantasia'],
];

// Nomes populares que não são da tabela — conferidos ANTES dos radicais ("azul marinho" ≠ "azul").
const APELIDOS: ReadonlyArray<[string, string, string]> = [
  ['marinho', 'Azul', '#1b2a4e'],
  ['celeste', 'Azul', '#6fa8dc'],
  ['grafite', 'Cinza', '#4a4f56'],
  ['chumbo', 'Cinza', '#4a4f56'],
  ['vinho', 'Grená', '#6e1a28'],
  ['bordo', 'Grená', '#6e1a28'],
  ['champa', 'Bege', '#d8c39a'],
  ['areia', 'Bege', '#d9c6a5'],
  ['creme', 'Bege', '#eadfc4'],
  ['ouro', 'Dourada', '#c8a24a'],
  ['lilas', 'Roxa', '#9a7bc4'],
  ['violeta', 'Roxa', '#6d3fa0'],
];

function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

export function resolverCorVeiculo(texto: string | null | undefined): CorResolvida {
  const t = normalizar(texto ?? '');
  if (!t) return { hex: COR_NEUTRA, base: null, reconhecida: false, fantasia: false };

  const hex = t.match(/^#?([0-9a-f]{6}|[0-9a-f]{3})$/);
  if (hex) {
    const h = hex[1].length === 3 ? hex[1].replace(/./g, (c) => c + c) : hex[1];
    return { hex: `#${h}`, base: null, reconhecida: true, fantasia: false };
  }

  let base: CorRenavam | null = null;
  let cor: string | null = null;
  const apelido = APELIDOS.find(([radical]) => t.includes(radical));
  if (apelido) {
    base = porNome(apelido[1]);
    cor = apelido[2];
  } else {
    const radical = RADICAIS.find(([r]) => t.includes(r));
    if (radical) {
      base = porNome(radical[1]);
      cor = base.hex;
    }
  }
  if (!base || !cor) return { hex: COR_NEUTRA, base: null, reconhecida: false, fantasia: false };

  if (/escur/.test(t)) cor = escurecer(cor, 0.3);
  else if (/clar/.test(t)) cor = clarear(cor, 0.35);

  return { hex: cor, base, reconhecida: true, fantasia: base.nome === 'Fantasia' };
}

// ── Aritmética de cor ───────────────────────────────────────────────────────

function rgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function paraHex([r, g, b]: [number, number, number]): string {
  return `#${[r, g, b].map((c) => Math.round(c).toString(16).padStart(2, '0')).join('')}`;
}

/** Mistura `a` com `b`; `t` é a fração de `b` (0 = só a, 1 = só b). */
export function misturar(a: string, b: string, t: number): string {
  const x = rgb(a);
  const y = rgb(b);
  return paraHex([0, 1, 2].map((i) => x[i] + (y[i] - x[i]) * t) as [number, number, number]);
}

export function escurecer(hex: string, t: number): string {
  return misturar(hex, '#000000', t);
}

export function clarear(hex: string, t: number): string {
  return misturar(hex, '#ffffff', t);
}

/** Luminância relativa (WCAG), 0 = preto, 1 = branco. */
export function luminancia(hex: string): number {
  const [r, g, b] = rgb(hex).map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Contorno que separa a pintura do fundo branco — carro branco não some na tela. */
export function contornoDaCor(hex: string): string {
  return escurecer(hex, luminancia(hex) > 0.6 ? 0.42 : 0.35);
}
