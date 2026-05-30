/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL da API FHIR. Em dev usa o proxy /fhir-api; em prod, valor do build. */
  readonly VITE_FHIR_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
