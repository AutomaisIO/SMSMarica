import type { Config } from 'tailwindcss';

/**
 * Tokens do painel do Secretário — ver front/README.md.
 * Neutros frios (papel/painel/tinta/grafite/linha), identidade SMS Maricá
 * (vermelho-marica só para marca/pulso/destaque ativo), séries de gráfico
 * (vinho principal, neutro-serie comparação) e semânticas de triagem Manchester
 * (entidade = cor; validadas com o validador de paleta do dataviz).
 */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        papel: '#FFFFFF',
        painel: '#F5F6F8',
        tinta: '#131A22',
        grafite: '#5A6572',
        linha: '#E6E9EE',
        grade: '#EDF0F3',
        'vermelho-marica': '#C8102E',
        vinho: '#9E1B32',
        'neutro-serie': '#C9CED6',
        triagem: {
          vermelho: '#D62828',
          'vermelho-forte': '#A31414',
          amarelo: '#E9A400',
          'amarelo-forte': '#B37E00',
          'amarelo-apoio': '#8A6100',
          verde: '#2E9E5B',
          'verde-forte': '#1F7A43',
          azul: '#2F6FDE',
          'azul-forte': '#1F51AB',
          cinza: '#8494A8',
          'cinza-forte': '#5F7186',
        },
      },
      fontFamily: {
        display: ['Archivo', 'system-ui', 'sans-serif'],
        corpo: ['"Instrument Sans"', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        cartao: '0 1px 2px rgba(19, 26, 34, 0.05)',
        flutuante: '0 8px 24px rgba(19, 26, 34, 0.10), 0 2px 6px rgba(19, 26, 34, 0.06)',
      },
      maxWidth: {
        pagina: '1180px',
      },
    },
  },
  plugins: [],
} satisfies Config;
