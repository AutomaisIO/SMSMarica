import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';

// Identificador único do build — SHA do commit no CI, timestamp no build local.
// Vai para dentro do bundle (define __VERSAO_BUILD__) E para /versao.json no dist;
// o app compara os dois em runtime e se auto-atualiza após um deploy
// (ver src/lib/useVersaoApp.ts). Mesmo mecanismo do painel smsmarica.
const versaoBuild = process.env.GITHUB_SHA?.slice(0, 12) ?? `local-${Date.now()}`;

function gerarVersaoJson(): Plugin {
  return {
    name: 'gerar-versao-json',
    apply: 'build',
    generateBundle() {
      this.emitFile({
        type: 'asset',
        fileName: 'versao.json',
        source: JSON.stringify({ build: versaoBuild }),
      });
    },
  };
}

export default defineConfig({
  define: {
    __VERSAO_BUILD__: JSON.stringify(versaoBuild),
  },
  plugins: [
    react(),
    gerarVersaoJson(),
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
        // Splash na cor da marca (mesmo padrão do app do cidadão).
        background_color: '#C8102E',
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
        // dado clínico-operacional é sempre rede. /versao.json idem: é justamente
        // o arquivo que denuncia um bundle velho, cacheá-lo cegaria a atualização.
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api/, /^\/versao\.json$/],
        globPatterns: ['**/*.{js,css,html,png,woff2}'],
        globIgnores: ['versao.json'],
        // O SW novo assume o controle na hora, sem esperar todas as abas fecharem.
        cleanupOutdatedCaches: true,
        skipWaiting: true,
        clientsClaim: true,
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
