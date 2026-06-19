import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        marica: {
          DEFAULT: '#C8102E',
          escuro: '#9e0c24',
        },
      },
    },
  },
  plugins: [],
} satisfies Config;
