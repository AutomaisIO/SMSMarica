import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Proxy de dev: /fhir-api → API FHIR local (Automais.Fhir.Api).
    proxy: {
      '/fhir-api': {
        target: 'http://localhost:5081',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/fhir-api/, ''),
      },
    },
  },
})
