import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // favicon e páginas legais estáticas (privacidade/termos) entram no bundle.
      includeAssets: ['favicon.svg', 'icon.svg', 'privacidade/index.html', 'termos/index.html'],
      manifest: {
        name: 'App do Paciente — SMS Maricá',
        short_name: 'SMS Paciente',
        description: 'Acompanhe seu transporte e tratamento (TFD) da Saúde de Maricá.',
        lang: 'pt-BR',
        theme_color: '#C8102E',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          { src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any maskable' },
        ],
      },
      workbox: {
        // as páginas legais são HTML real servido pelo nginx — não cair no SPA fallback.
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/privacidade/, /^\/termos/, /^\/api/],
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2}'],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
});
