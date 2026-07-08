/// <reference types="vite/client" />

/** Id do build injetado pelo vite.config.ts (define) — par do /versao.json do dist. */
declare const __VERSAO_BUILD__: string;

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
