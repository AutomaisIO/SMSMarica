import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// Identificador único do build — SHA do commit no CI, timestamp no build local.
// Vai para dentro do bundle (define __VERSAO_BUILD__) E para /versao.json no dist;
// o app compara os dois em runtime para se auto-atualizar após um deploy
// (ver shared/hooks/useVersaoApp.ts).
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
  plugins: [react(), gerarVersaoJson()],
  define: {
    __VERSAO_BUILD__: JSON.stringify(versaoBuild),
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  // Cornerstone3D usa web workers ES module + codecs WASM; excluí-lo do
  // pre-bundle e usar workers em formato ES evita quebras de empacotamento.
  optimizeDeps: {
    exclude: ['@cornerstonejs/dicom-image-loader'],
  },
  worker: {
    format: 'es',
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        rewrite: (p) => p.replace(/^\/api/, ''),
      },
      // Hubs SignalR (WebSocket) — chat em tempo real. `ws: true` faz o upgrade.
      '/hubs': {
        target: 'http://localhost:5080',
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
