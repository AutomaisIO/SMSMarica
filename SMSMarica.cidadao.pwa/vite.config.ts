import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // favicon, ícones de instalação e páginas legais estáticas entram no bundle.
      includeAssets: [
        'favicon.svg',
        'icon.svg',
        'icon-192.png',
        'icon-512.png',
        'apple-touch-icon.png',
        'privacidade/index.html',
        'termos/index.html',
      ],
      manifest: {
        name: 'App do Cidadão — SMS Maricá',
        short_name: 'SMS Cidadão',
        description: 'Saúde de Maricá no seu bolso: agende consultas e exames, acesse seus documentos de saúde e acompanhe seu transporte (TFD).',
        lang: 'pt-BR',
        theme_color: '#C8102E',
        background_color: '#ffffff',
        display: 'standalone',
        orientation: 'portrait',
        start_url: '/',
        scope: '/',
        // PNGs 192/512 (recomendado p/ Android) + SVG escalável. A cruz tem margem
        // suficiente p/ servir como maskable (não corta no recorte circular).
        icons: [
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          { src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        ],
      },
      workbox: {
        // as páginas legais são HTML real servido pelo nginx — não cair no SPA fallback.
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/privacidade/, /^\/termos/, /^\/api/],
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2}'],
        // ao publicar versão nova, limpa precaches antigos (evita app preso em versão).
        cleanupOutdatedCaches: true,
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
