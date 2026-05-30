# Automais.Fhir.front

Front do hub **Automais FHIR** — React + Vite + TypeScript. Consome a API FHIR (`Automais.Fhir.Api`).

## Setup

```bash
cd Automais.Fhir/Automais.Fhir.front
npm install
cp .env.example .env   # ajuste se a API não estiver em localhost:5081
npm run dev            # http://localhost:5173
```

Em dev, o Vite faz proxy de `/fhir-api` → `http://localhost:5081` (a API FHIR local). Em produção, defina `VITE_FHIR_API_BASE_URL` no build (ou sirva front e API sob a mesma origem via nginx).

## Estrutura

```
src/
├── main.tsx              # bootstrap React
├── App.tsx              # tela inicial (busca de Patient)
└── api/
    └── fhirClient.ts    # cliente fetch (application/fhir+json, OperationOutcome)
```

## Produto portável

Este front faz parte do **Automais.Fhir**, produto FHIR reutilizável por qualquer prefeitura — não é específico de Maricá.
