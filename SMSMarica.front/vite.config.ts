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

/**
 * Cada instância aponta para o próprio backend (ADR-0043 — uma instância por município),
 * e o painel só sabe qual por `VITE_API_BASE_URL`.
 *
 * A checagem é aqui, e não só no `httpClient`, porque um `throw` em módulo do bundle só
 * aparece no navegador — tela branca depois do deploy. Falhar no build é barulhento, é
 * barato, e acontece antes de qualquer artefato subir.
 *
 * Até 2026-08 havia um fallback para `https://api.smsmarica.online`: um build sem a env
 * subia calado e apontava o painel de um município para o backend de Maricá.
 */
function exigirApiBaseUrl(modo: string) {
  if (modo !== 'production') return;
  if (process.env.VITE_API_BASE_URL?.trim()) return;
  throw new Error(
    'VITE_API_BASE_URL não definida.\n' +
      'O painel de cada município aponta para o backend daquele município — não há default.\n' +
      'Defina a variável no ambiente de build (no CI, o secret da instância).',
  );
}

export default defineConfig(({ mode }) => {
  exigirApiBaseUrl(mode);
  return {
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
  };
});
