import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Civismo Maricá
        marica: { DEFAULT: '#C8102E', escuro: '#A00C24' },
        vinho: '#6E1322',
        // Superfícies quentes (papel de documento, não cinza-frio)
        papel: '#FBF8F6',
        areia: '#ECE3E1',
        tinta: { DEFAULT: '#201A1B', mute: '#6F6466' },
        // Afordância clínica (orla/lagoas de Maricá)
        lagoa: { DEFAULT: '#0E7C7B', escuro: '#0A5E5D', claro: '#E5F2F1' },
      },
      fontFamily: {
        display: ['"Bricolage Grotesque Variable"', 'system-ui', 'sans-serif'],
        sans: ['"Inter Variable"', 'system-ui', '-apple-system', 'sans-serif'],
      },
      borderRadius: {
        xl: '0.875rem',
        '2xl': '1.25rem',
        '3xl': '1.75rem',
      },
      boxShadow: {
        carta: '0 1px 2px rgba(32,26,27,0.04), 0 8px 24px -12px rgba(32,26,27,0.18)',
        cartao: '0 18px 40px -20px rgba(110,19,34,0.55)',
        topo: '0 2px 12px -6px rgba(32,26,27,0.25)',
      },
      keyframes: {
        'slide-in': { from: { transform: 'translateX(-100%)' }, to: { transform: 'translateX(0)' } },
        'fade-in': { from: { opacity: '0' }, to: { opacity: '1' } },
        'rise': { from: { opacity: '0', transform: 'translateY(8px)' }, to: { opacity: '1', transform: 'translateY(0)' } },
      },
      animation: {
        'slide-in': 'slide-in .22s cubic-bezier(.22,.61,.36,1)',
        'fade-in': 'fade-in .2s ease-out',
        'rise': 'rise .35s cubic-bezier(.22,.61,.36,1) both',
      },
    },
  },
  plugins: [],
} satisfies Config;
