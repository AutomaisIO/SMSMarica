import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: [
        'favicon.png',
        'icon-192.png',
        'icon-512.png',
        'icon-512-maskable.png',
        'apple-touch-icon.png',
        'marica_logo.png',
      ],
      manifest: {
        id: '/',
        name: 'Painel da Saúde — Maricá',
        short_name: 'Painel Saúde',
        description:
          'Painel executivo da Secretaria de Saúde de Maricá — números ao vivo do Hospital Conde Modesto Leal.',
        lang: 'pt-BR',
        theme_color: '#C8102E',
        background_color: '#FFFFFF',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: '/icon-512-maskable.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // SPA fallback só para navegação; /api NUNCA cai no fallback nem em cache —
        // dado clínico-operacional é sempre rede.
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api/],
        globPatterns: ['**/*.{js,css,html,png,woff2}'],
        cleanupOutdatedCaches: true,
      },
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  build: {
    // recharts sozinho passa de 500 kB minificado — já isolado em chunk próprio.
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      output: {
        // recharts é o maior pedaço do bundle — chunk próprio melhora o cache.
        manualChunks: { graficos: ['recharts'] },
      },
    },
  },
  server: {
    port: 5175,
    proxy: {
      // sem rewrite — o back já serve /api/painel
      '/api': {
        target: 'http://localhost:5090',
        changeOrigin: true,
      },
    },
  },
});
