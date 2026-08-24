import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
import path from 'node:path';
import { readFileSync } from 'node:fs';

// Versão exibida no app (controle interno / suporte). Fonte única: package.json.
const { version: APP_VERSION } = JSON.parse(
  readFileSync(new URL('./package.json', import.meta.url), 'utf-8'),
);

export default defineConfig(({ mode }) => {
  // Identidade por INSTÂNCIA via env de build (ADR-0046): default NEUTRO — o deploy de cada
  // município injeta os valores dele; nenhum nome/cor de cidade fica no código.
  const env = loadEnv(mode, process.cwd(), '');
  const NOME = env.VITE_APP_NOME || 'Arquivos Saúde';
  const NOME_CURTO = env.VITE_APP_NOME_CURTO || 'Arquivos';
  const COR = env.VITE_TEMA_COR || '#475569';
  const FUNDO = env.VITE_TEMA_FUNDO || '#334155';
  return {
  define: {
    __APP_VERSION__: JSON.stringify(APP_VERSION),
  },
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      // favicon e ícones de instalação entram no bundle.
      includeAssets: [
        'favicon.png',
        'icon-192.png',
        'icon-512.png',
        'icon-512-maskable.png',
        'apple-touch-icon.png',
      ],
      manifest: {
        name: NOME,
        short_name: NOME_CURTO,
        description:
          'Digitalize e envie exames antigos pela câmera do celular, lendo o QR code mostrado na clínica.',
        lang: 'pt-BR',
        theme_color: COR,
        background_color: FUNDO,
        display: 'standalone',
        orientation: 'portrait',
        start_url: '/',
        scope: '/',
        // PNGs 192/512 (recomendado p/ Android). A versão maskable tem a arte a ~80%
        // sobre fundo vermelho, com margem de segurança p/ o recorte circular do Android.
        icons: [
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: '/icon-512-maskable.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api/],
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
    port: 5175,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
    },
  },
};
});
