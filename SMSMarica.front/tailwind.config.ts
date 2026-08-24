import type { Config } from 'tailwindcss';

/** Passos da escala, iguais aos do Tailwind. */
const PASSOS = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950] as const;

/**
 * Monta a escala 50..950 apontando para as variáveis CSS `--c-<nome>-<passo>`
 * (definidas em `src/index.css`). Ver ADR-0043.
 */
const escala = (nome: string): Record<string, string> =>
  Object.fromEntries(
    PASSOS.map((passo) => [passo, `rgb(var(--c-${nome}-${passo}) / <alpha-value>)`]),
  );

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        /*
         * Cor da marca por variável CSS (ADR-0043): a paleta real mora em `src/index.css`
         * e pode ser trocada em runtime pela identidade da instituição, sem rebuild.
         *
         * O formato `rgb(var(--x) / <alpha-value>)` não é capricho — é o que preserva os
         * modificadores de opacidade (`bg-primary-600/40`). Com `var(--x)` direto no lugar
         * do hex, eles quebrariam em silêncio.
         *
         * Os 97 arquivos que já usam `*-primary-*` / `*-secondary-*` passam a seguir a
         * marca do cliente sem precisar de uma linha de mudança.
         */
        primary: escala('primary'),
        secondary: escala('secondary'),
        accent: {
          50: '#FFF7ED',
          100: '#FFEDD5',
          200: '#FED7AA',
          300: '#FDBA74',
          400: '#FB923C',
          500: '#F97316',
          600: '#EA580C',
          700: '#C2410C',
          800: '#9A3412',
          900: '#7C2D12',
        },
        gray: {
          50: '#fafafa',
          100: '#f4f4f5',
          200: '#e4e4e7',
          300: '#d4d4d8',
          400: '#a1a1aa',
          500: '#71717a',
          600: '#52525b',
          700: '#3f3f46',
          800: '#27272a',
          900: '#18181b',
          950: '#09090b',
        },
        success: {
          500: '#10b981',
          600: '#059669',
        },
        warning: {
          500: '#f59e0b',
          600: '#d97706',
        },
        error: {
          500: '#ef4444',
          600: '#dc2626',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      // Tokens de marca. Chamavam-se `marica`/`gradient-marica` e carregavam o vermelho
      // fixo; agora derivam da paleta, então acompanham a cor do cliente (ADR-0043).
      boxShadow: {
        marca: '0 4px 14px 0 rgb(var(--c-primary-700) / 0.30)',
        'marca-lg': '0 10px 40px 0 rgb(var(--c-primary-900) / 0.35)',
      },
      backgroundImage: {
        'gradient-marca':
          'linear-gradient(145deg, rgb(var(--c-primary-700)) 0%, rgb(var(--c-primary-900)) 55%, rgb(var(--c-primary-950)) 100%)',
        'gradient-marca-soft':
          'linear-gradient(135deg, rgb(var(--c-primary-600)) 0%, rgb(var(--c-primary-700)) 100%)',
      },
    },
  },
  plugins: [],
} satisfies Config;
