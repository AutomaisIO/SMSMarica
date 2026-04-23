import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Vermelho Maricá (referência: logo prefeitura).
        primary: {
          50: '#FEF2F3',
          100: '#FDE3E6',
          200: '#FAC8CE',
          300: '#F59FA9',
          400: '#EE6B7B',
          500: '#E03C52',
          600: '#C8102E',
          700: '#A80C27',
          800: '#870A20',
          900: '#6C0819',
          950: '#3D040E',
        },
        secondary: {
          50: '#FFF1F2',
          100: '#FFE0E3',
          200: '#FFC2C8',
          300: '#FB8E97',
          400: '#EE5764',
          500: '#A80C27',
          600: '#870A20',
          700: '#6C0819',
          800: '#570614',
          900: '#3D040E',
          950: '#240207',
        },
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
      boxShadow: {
        marica: '0 4px 14px 0 rgba(168, 12, 39, 0.30)',
        'marica-lg': '0 10px 40px 0 rgba(108, 8, 25, 0.35)',
      },
      backgroundImage: {
        'gradient-marica': 'linear-gradient(145deg, #A80C27 0%, #6C0819 55%, #3D040E 100%)',
        'gradient-marica-soft': 'linear-gradient(135deg, #C8102E 0%, #A80C27 100%)',
      },
    },
  },
  plugins: [],
} satisfies Config;
