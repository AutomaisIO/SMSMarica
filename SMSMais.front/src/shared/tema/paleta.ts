/**
 * Derivação da escala 50..950 a partir de UMA cor da marca (ADR-0043).
 *
 * Uma prefeitura informa um hex só; o painel precisa de onze tons. Em vez de pedir os onze
 * na tela de configuração — que ninguém preencheria bem —, os demais saem daqui, misturando
 * a cor base com branco (tons claros) e com preto (tons escuros).
 *
 * <b>Isto NÃO roda para quem não configurou cor.</b> A paleta de Maricá continua literal em
 * `index.css`, hand-tuned como sempre foi; a derivação só entra quando há `corPrimaria`.
 * Uma escala derivada é boa o bastante para um cliente novo, mas não é igual à ajustada à
 * mão — e trocar a de Maricá por uma aproximação seria uma regressão visual gratuita.
 */

/** Passos da escala, iguais aos do Tailwind. */
export const PASSOS = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950] as const;

/**
 * Quanto cada passo se afasta da base. Negativo = mistura com branco (clareia);
 * positivo = mistura com preto (escurece). A base é o passo 600, que é o tom
 * "cheio" usado em botão e destaque.
 *
 * Os fatores do lado escuro foram calibrados contra a paleta de Maricá (600→950), para
 * que uma cor parecida com a dela produza uma escala parecida com a dela.
 */
const MISTURA: Record<(typeof PASSOS)[number], number> = {
  50: -0.95,
  100: -0.89,
  200: -0.76,
  300: -0.58,
  400: -0.35,
  500: -0.17,
  600: 0,
  700: 0.16,
  800: 0.33,
  900: 0.46,
  950: 0.7,
};

type Rgb = readonly [number, number, number];

/** `#RRGGBB` → canais. Devolve null se o formato não bater — nunca lança. */
export function lerHex(hex: string | null | undefined): Rgb | null {
  if (!hex) return null;
  const m = /^#([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return null;
  const n = parseInt(m[1], 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

const limitar = (v: number) => Math.max(0, Math.min(255, Math.round(v)));

/** Mistura com branco (t < 0) ou com preto (t > 0). */
function misturar([r, g, b]: Rgb, t: number): Rgb {
  const alvo = t < 0 ? 255 : 0;
  const p = Math.abs(t);
  return [
    limitar(r + (alvo - r) * p),
    limitar(g + (alvo - g) * p),
    limitar(b + (alvo - b) * p),
  ];
}

/**
 * Escala completa como triplets `"R G B"`, no formato que as variáveis CSS esperam
 * (ver `src/index.css` e `tailwind.config.ts`).
 */
export function derivarEscala(baseHex: string): Record<number, string> | null {
  const base = lerHex(baseHex);
  if (!base) return null;
  return Object.fromEntries(
    PASSOS.map((passo) => {
      const [r, g, b] = misturar(base, MISTURA[passo]);
      return [passo, `${r} ${g} ${b}`];
    }),
  );
}

/** `#RRGGBB` a partir de um triplet `"R G B"` — para as vars `--theme-*`, que são hex. */
export function paraHex(triplet: string): string {
  const [r, g, b] = triplet.split(' ').map(Number);
  return `#${[r, g, b].map((c) => c.toString(16).padStart(2, '0')).join('')}`.toUpperCase();
}
